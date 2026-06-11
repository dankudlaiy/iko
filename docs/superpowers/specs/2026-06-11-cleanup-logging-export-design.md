# iko — рефакторинг, логирование, Export, типизация (дизайн)

Дата: 2026-06-11. Статус: одобрено пользователем.

## Цель

Довести проект до «дипломного» качества: логирование и обработка ошибок, устранение
дублирования через общий интерфейс клиентов платформ, замена заглушек реальной фичей
Export, типизация фронтенда, серверная валидация, EF-миграции.

Ротация секретов (.env в git) — отложена до этапа деплоя, в этот объём не входит.

## 1. Общий интерфейс клиентов платформ

```csharp
public interface IPlatformClient
{
    Platform Platform { get; }
    Task<List<PlaylistSummary>> GetPlaylists(string accessToken);
    Task<List<TrackModel>> GetPlaylistTracks(string playlistId, string accessToken);
    Task<TrackModel?> SearchForTrack(string name, string artist, string? accessToken);
    Task<(string url, string? imageUrl)> CreatePlaylist(IEnumerable<string> trackIds, string accessToken, string name);
}
```

- Реализуют `SpotifyClient`, `YouTubeClient`, `AppleMusicClient`.
- Дублирующая Spotify-логика из `LibraryController` (`new HttpClient()`, `dynamic`)
  переезжает в `SpotifyClient` (методы `GetPlaylists`, `GetPlaylistTracks`).
- Все клиенты переводятся на `IHttpClientFactory` (`AddHttpClient<T>()`).
- `PlatformClientFactory` резолвит `Platform` → `IPlatformClient`; для неподдерживаемых
  платформ кидает `PlatformNotSupportedException`.
- Контроллеры (`LibraryController`, новый Export) используют фабрику вместо switch.
- Новая модель `PlaylistSummary` (id, name, imageUrl, trackCount) вместо анонимных объектов.

## 2. Логирование и обработка ошибок

- **Serilog** (`Serilog.AspNetCore`): консоль + rolling-файл `logs/iko-.log`
  (daily), `UseSerilogRequestLogging()`.
- `ILogger<T>` во всех клиентах и контроллерах.
- Пустые `catch { }` в `YouTubeClient` и глотание исключений в `SearchController`
  заменяются на `LogWarning` с контекстом (платформа, трек, HTTP-статус). Поведение
  graceful degradation (одна платформа упала — поиск продолжается) сохраняется.
- Глобальный обработчик: `IExceptionHandler` (.NET 8) + `AddProblemDetails()`.
  Необработанное исключение → лог + ProblemDetails 500.
  `PlatformApiException` (новое исключение, кидается клиентами при ошибках внешних
  API) → 502 с указанием платформы.
- Ожидаемые ошибки контроллеров остаются в формате `{ data, error }`.
- Исправить CORS в Program.cs: убрать противоречие `WithOrigins` + `AllowAnyOrigin`,
  оставить `WithOrigins("http://localhost:4200")`.

## 3. Export вместо /convert

Удаляется:
- маршрут `/convert` и `HomeComponent` (ts/html/css/spec);
- `PlaylistController` целиком и его DTO (`ParseRequest`, `SearchRequest`,
  `CreatePlaylistApiRequest`);
- методы `parsePlaylist`, `searchTracks`, `createExternalPlaylist` из `ApiService`.

Добавляется:
- `POST /api/iko-playlists/{id}/export`, тело `{ targetPlatform }`.
  Логика: трек с целевой платформы → берём `PlatformTrackId` напрямую; иначе
  `SearchForTrack(name, artist)`. Создаём плейлист через `IPlatformClient.CreatePlaylist`.
  Ответ: `{ url, matchedCount, totalCount, unmatchedTracks }`.
- UI в редакторе плейлиста: кнопка Export → дропдаун подключённых платформ →
  индикатор прогресса → диалог с ссылкой и списком ненайденных треков.

## 4. Зачистка SoundCloud/Deezer

- Убрать из списков платформ в settings, из `platform-badge`.
- Удалить заглушки `connect/soundcloud`, `connect/deezer` в `AccountsController`.
- Ветки switch исчезают вместе с переходом на фабрику.
- Значения enum `Platform` сохраняются (совместимость БД; в записке — «перспективы
  развития»).

## 5. Серверная валидация (DataAnnotations)

- `[Required]`, `[EmailAddress]`, `[MinLength(8)]` (пароль), `[MaxLength(100)]`
  (имена плейлистов) на request-DTO.
- `[ApiController]` даёт автоматический 400; дублирующие ручные проверки убрать.

## 6. Типизация фронтенда

- `src/app/models.ts`: `Track`, `IkoPlaylist`, `LibraryPlaylist`, `SearchResults`,
  `ConnectedAccount`, `UserInfo`, `ExportResult`,
  `ApiResponse<T> = { data: T | null; error: string | null }`.
- `ApiService` полностью типизирован; `any` в компонентах заменяется интерфейсами.

## 7. Миграции EF

- Создать миграцию `InitialCreate`; `EnsureCreated()` → `Migrate()` в Program.cs.
- Локальный `iko.db` удаляется и пересоздаётся миграцией (одобрено пользователем —
  локальные тестовые данные не нужны).

## Порядок реализации

1. Бэкенд-рефакторинг: `IPlatformClient`, перенос Spotify-логики, `IHttpClientFactory`,
   фабрика, переписать `LibraryController`.
2. Serilog + `IExceptionHandler` + CORS-фикс.
3. Export-эндпоинт + удаление PlaylistController.
4. Фронтенд: удалить /convert и HomeComponent, Export UI, зачистка SoundCloud/Deezer.
5. Типизация (`models.ts`, ApiService, компоненты).
6. DataAnnotations.
7. Миграции EF, пересоздание dev-БД.

После каждого блока — сборка `dotnet build` / `ng build`; в конце ручная проверка
ключевых флоу (библиотека, редактор, export, поиск).
