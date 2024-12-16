var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        policy  =>
        {
            policy.WithOrigins("http://localhost:4200", "https://localhost:44389")
                .AllowAnyOrigin()
                .AllowAnyMethod();
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

/*
 public Form1()
    {
        InitializeComponent();
            
        DotNetEnv.Env.Load();
            
        var textBox = new TextBox
        {
            Location = new Point(15, 15),
            Width = 200,
            PlaceholderText = "vk playlist url"
        };
            
        Controls.Add(textBox);
            
        var button = new Button
        {
            Text = "parse",
            Location = new Point(15, 45),
        };
            
        var spotifyClient = new SpotifyClient();
        var vkParser = new VkParser();

        var trackModels = new List<TrackModel>();
            
        button.Click += async (_, _) =>
        { 
            var tracks = await vkParser.ParseVkPlaylist(textBox.Text);
                
            trackModels.Clear();

            var proceedTracks = 0;
                
            foreach (var track in tracks)
            {
                var spotifySearchResponse = await spotifyClient.SearchForTrack($"{track.Name} {track.Artist}");

                if (spotifySearchResponse is null)
                {
                    proceedTracks++; continue;
                }
                        
                spotifySearchResponse.Name = track.Name;
                spotifySearchResponse.Artist = track.Artist;

                trackModels.Add(spotifySearchResponse);

                proceedTracks++;

                textBox.Text = $"proceedTracks: {proceedTracks}/{tracks.Count}";
            }
                
            var trackListPanel = new FlowLayoutPanel 
            {
                Location = new Point(15, 75),
                Size = new Size(400, 1000),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };

            foreach (var track in trackModels)
            {
                var pictureBox = new PictureBox 
                {
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(50, 50),
                    Location = new Point(5,5),
                };
    
                using (var client = new WebClient())
                {
                    var imageBytes = client.DownloadData(track.ImageUrl);
                    using (var ms = new MemoryStream(imageBytes))
                    {
                        pictureBox.Image = Image.FromStream(ms);
                    }
                }
                
                var label = new Label 
                {
                    Text = track.Name,
                    AutoSize = true,
                    Location = new Point(pictureBox.Right + 5, pictureBox.Top),
                };
                
                var panel = new Panel 
                {
                    AutoSize = false,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                };
    
                panel.Controls.Add(pictureBox);
                panel.Controls.Add(label);
                    
                trackListPanel.Controls.Add(panel);
            };
                
            Controls.Add(trackListPanel);
                
            var proceedButton = new Button
            {
                Text = "create playlist",
                Location = new Point(600, 500),
            };

            proceedButton.Click += async (_, _) =>
            {
                var scopes = Uri.EscapeDataString("user-read-private user-read-email playlist-modify-public playlist-modify-private");
                    
                var clientId = Environment.GetEnvironmentVariable("CLIENTID") ?? 
                               throw new InvalidOperationException("Can not retrieve client id from .env file");
                    
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"https://accounts.spotify.com/authorize?client_id={clientId}&response_type=code&redirect_uri=" 
                               + Uri.EscapeDataString(SpotifyClient.RedirectUri) + $"&scope={scopes}",
                    UseShellExecute = true
                });

                while (Code is null)
                {
                    await Task.Delay(1000);
                }

                var spotifyTokenResponse = await spotifyClient.ObtainAccessToken(Code);
                    
                var playlistUrl = await spotifyClient.CreatePlaylist(trackModels, spotifyTokenResponse.access_token);
                    
                MessageBox.Show($"Playlist created! You can access it at {playlistUrl}");

                textBox.Text = playlistUrl;
            };
                
            Controls.Add(proceedButton);
        };
            
        Controls.Add(button);
    }
 */