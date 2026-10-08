# Трекер экзаменационных тем

Защита: 13 октября 2026 года. [План подготовки](preparation-plan.md). [Точные английские формулировки всех тем](../../bachelor-exam-topics-2025-2026.md).

Оценка: **—** не проверено; **0** не могу ответить; **1** есть существенные пробелы; **2** правильный ответ и пример с подсказками; **3** самостоятельный ответ, пример и дополнительный вопрос. Примеры ниже — задания для тренировки, а не полный конспект и не готовые ответы.

Колонки «Проверка» и «Повторить» заполняем датой фактического ответа и ближайшего повторения. Ошибки фиксируем в журнале под таблицей. Все ответы — на английском; русский используем для прояснения смысла.

| № | Topic | Пример на бумаге или доске | День | Оценка | Проверка | Повторить |
|---:|---|---|---|:---:|---|---|
| 1 | Real sequences, convergence, Cauchy criterion | `1/n → 0`: определение с ε и N; объяснить Cauchy criterion в ℝ и пример расходимости | Пт | — | — | — |
| 2 | Matrices, operations, rank, determinant | Для матрицы 2×2 найти determinant и rank; показать умножение и условие совместимости размеров | Пт | — | — | — |
| 3 | Linear systems | Два уравнения с двумя неизвестными, Gaussian elimination; unique/no/infinite solutions | Пт | — | — | — |
| 4 | Propositional calculus, tautologies | Таблица истинности `p ∨ ¬p` и пример формулы, которая не tautology | Пт | — | — | — |
| 5 | Mathematical induction | Доказать `1 + … + n = n(n+1)/2`: base case, hypothesis, step | Пт | — | — | — |
| 6 | Permutations, variations, combinations | Выбрать двух из пяти с учётом и без учёта порядка; варианты с повторением и без | Пт | — | — | — |
| 7 | Classical and geometric probability | Честный кубик и равномерная точка на отрезке; явно назвать sample space и предпосылку равномерности | Пт | — | — | — |
| 8 | Positional, binary, hexadecimal systems | Перевести `45` в binary/hex и обратно; связь четырёх бит с hex digit | Пт | — | — | — |
| 9 | Fixed-point, floating-point, representation | Масштабированный integer для сотых; two's complement; sign/exponent/significand; `0.1` и ошибка округления | Пт | — | — | — |
| 10 | OS as seen by applications | `open/read/write`, system call, process, memory и разделение user/kernel mode | Сб | — | — | — |
| 11 | Traditional Unix | Process, file descriptor, permissions, pipe `cat file.txt \| sort` и fork/exec | Сб | — | — | — |
| 12 | Iteration and recursion | Factorial двумя способами; base case, call stack, time/space | Чт | — | — | — |
| 13 | Structured and object-oriented programming | Одна задача через функции и через объект; состояние, поведение и критерий выбора | Чт | — | — | — |
| 14 | Simple/complex types, pointers | Scalar, array, record; нарисовать address, pointer, dereference и null pointer | Чт | — | — | — |
| 15 | Encapsulation | Private balance и deposit с проверкой инварианта; почему private field сам по себе недостаточен | Чт | — | — | — |
| 16 | Abstraction, inheritance, polymorphism | `IPlatformClient` и две реализации; отдельно показать class inheritance на маленьком примере | Чт | — | — | — |
| 17 | Conditions and loops | `if/else`, `for`, `while`; сравнить precondition/postcondition loop | Чт | — | — | — |
| 18 | Subroutines and parameter passing | Passing by value/reference; отличить копирование ссылки на объект от передачи параметра by reference | Чт | — | — | — |
| 19 | Vector and raster graphics | Circle как shape и как pixels; изменение масштаба, достоинства и ограничения | Сб | — | — | — |
| 20 | Graphic file formats | JPEG, PNG, GIF, SVG: raster/vector, compression, transparency, animation, применение | Сб | — | — | — |
| 21 | Color models | RGB, CMYK, HSV: экран, печать, выбор цвета; additive/subtractive | Сб | — | — | — |
| 22 | Elementary/non-elementary sorting | Insertion sort и merge sort на `[4,1,3,2]`; time, extra memory, stability | Чт | — | — | — |
| 23 | Linked lists and trees | Узлы списка и BST; вставка/поиск, balanced vs degenerate tree, применение | Чт | — | — | — |
| 24 | Stacks and queues | push/pop и enqueue/dequeue; array/linked implementation, LIFO/FIFO | Чт | — | — | — |
| 25 | Graphs and traversal | Нарисовать граф из пяти вершин; adjacency list/matrix, BFS и DFS с visited | Пт | — | — | — |
| 26 | Linear, binary, hash search | Один набор данных тремя способами; sorted input для binary search и collisions для hashing | Пт | — | — | — |
| 27 | Computational complexity | Один цикл, два вложенных цикла и деление диапазона пополам; time/space, average/worst case | Чт | — | — | — |
| 28 | Database concepts and capabilities | Playlist storage; queries, persistence, constraints, transactions, DB vs DBMS | Сб | — | — | — |
| 29 | Relations and attributes | Таблица `Users(id,email)`; relation, tuple, attribute, domain, key | Сб | — | — | — |
| 30 | Referential integrity | `Tracks.PlaylistId` → `Playlists.Id`; orphan row, delete restrictions/cascade | Сб | — | — | — |
| 31 | Normalization and normal forms | Маленькая таблица заказов: functional dependencies, 1NF/2NF/3NF, update anomaly; идея BCNF | Сб | — | — | — |
| 32 | Relationships, primary/foreign keys | User→playlists, playlist→tracks; отдельно many-to-many через join table | Сб | — | — | — |
| 33 | Index types and applications | Unique email index, composite index; B-tree/hash, чтение против стоимости записи | Сб | — | — | — |
| 34 | Basic SQL | `SELECT`, `WHERE`, `JOIN`, `GROUP BY`, `HAVING`, `ORDER BY`; по примеру INSERT/UPDATE/DELETE | Вс | — | — | — |
| 35 | ISO OSI layers | Нарисовать семь уровней, функции и примеры; пройти путь запроса сверху вниз | Вс | — | — | — |
| 36 | Logical addressing | IPv4/CIDR: `192.168.1.10/24`, network/host; IP vs MAC, basic IPv6 idea | Вс | — | — | — |
| 37 | TCP/IP family | Application/transport/internet/link; TCP vs UDP, IP, DNS, HTTP и адресация портами | Вс | — | — | — |
| 38 | Software life cycles | Requirements→design→implementation→testing→deployment→maintenance; waterfall vs iterative | Вс | — | — | — |
| 39 | Testing process and role | Requirements→test cases→execution→defects→retest/regression; unit/integration/system/acceptance | Вс | — | — | — |
| 40 | UML structure and purpose | Class diagram и sequence diagram для connect account; structural vs behavioral diagrams | Вс | — | — | — |
| 41 | Project roles and responsibilities | Analyst, manager/owner, designer, developer, tester, operations; роли могут совмещаться | Вс | — | — | — |
| 42 | AI principles and applications | Поиск и rules; supervised/unsupervised/reinforcement learning; data, objective, training/inference, применение | Вс | — | — | — |
| 43 | Neural networks | `y = activation(Σwᵢxᵢ+b)`; layers, loss, gradient descent/backpropagation, overfitting | Вс | — | — | — |
| 44 | Automata, machines, languages, grammars | DFA для even number of ones; PDA для balanced parentheses; Turing tape; regular/type 3, context-free/type 2, recursively enumerable/type 0 | Вс | — | — | — |

## Журнал ошибок и повторений

Учебные ответы пока не проверялись. После каждой сессии записываем: дату, номер темы, конкретную ошибку, короткое исправление по-английски и результат повторного ответа.

## Где остановились

План составлен 8 октября. Следующая сессия — день 1: минутное введение о дипломе, архитектура, темы 12–18, 22–24, 27. Выполнение учебных блоков ещё не начато.
