using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.ContextMenu
{
    public record PlaceInfo(long Id, long UniverseId, string Name, string? ThumbnailUrl);
    internal partial class GameInformationViewModel : ObservableObject
    {
        private readonly long _placeId;
        private readonly long _universeId;

        [ObservableProperty]
        private string _gameName = "Loading...";

        [ObservableProperty]
        private string _gameDescription = "";

        [ObservableProperty]
        private Bitmap? _gameThumbnail;

        [ObservableProperty]
        private string _creatorName = "Loading...";

        [ObservableProperty]
        private bool _isCreatorVerified;

        [ObservableProperty]
        private long _playingCount;

        [ObservableProperty]
        private long _visitsCount;

        [ObservableProperty]
        private long _favoritesCount;

        [ObservableProperty]
        private string _createdDate = "";

        [ObservableProperty]
        private string _updatedDate = "";

        [ObservableProperty]
        private string _genre = "";

        [ObservableProperty]
        private bool _isDataLoading = true;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private ObservableCollection<PlaceInfo> _subplaces = new();

        [ObservableProperty]
        private bool _hasSubplaces;

        public GameInformationViewModel(long placeId, long universeId)
        {
            _placeId = placeId;

            _universeId = universeId;

            if (_placeId > 0)
            {
                _ = LoadGameInformationAsync();
            }
            else
            {
                HasError = true;
                ErrorMessage = "No Game Found.";
                IsDataLoading = false;
            }
        }

        private async Task LoadGameInformationAsync()
        {
            const string LOG_IDENT = "GameInformationViewModel::LoadGameInformation";

            try
            {
                IsDataLoading = true;
                HasError = false;

                if (_placeId == 0)
                {
                    throw new InvalidOperationException("No place ID provided");
                }

                long universeId = _universeId;

                if (universeId == 0)
                {
                    throw new InvalidOperationException("Could not determine game universe ID");
                }

                await LoadUniverseDetailsAsync(universeId);
                await LoadSubplacesAsync(universeId);

                string thumbnailUrl = await GetPlaceThumbnailUrlAsync(_placeId);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    GameThumbnail = await LoadBitmapFromUrlAsync(thumbnailUrl);
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error loading game information: {ex.Message}");
                HasError = true;
                ErrorMessage = $"Failed to load game information: {ex.Message}";
            }
            finally
            {
                IsDataLoading = false;
            }
        }

        private async Task LoadUniverseDetailsAsync(long universeId)
        {
            const string LOG_IDENT = "GameInformationViewModel::LoadUniverseDetails";

            try
            {
                await UniverseDetails.FetchBulk(universeId.ToString());
                var universeDetails = UniverseDetails.LoadFromCache(universeId);

                if (universeDetails?.Data != null)
                {
                    var data = universeDetails.Data;

                    GameName = data.Name ?? "Unknown Game";
                    GameDescription = data.Description ?? "No description available";

                    if (data.Creator != null)
                    {
                        CreatorName = data.Creator.Name ?? "Unknown Creator";
                        IsCreatorVerified = data.Creator.HasVerifiedBadge;
                    }
                    else
                    {
                        CreatorName = "Unknown Creator";
                        IsCreatorVerified = false;
                    }

                    PlayingCount = data.Playing;
                    VisitsCount = data.Visits;
                    FavoritesCount = data.FavoritedCount;

                    Genre = GetGenreName(data.Genre);

                    CreatedDate = data.Created.ToString("MMMM dd, yyyy");
                    UpdatedDate = data.Updated.ToString("MMMM dd, yyyy");
                }
                else
                {
                    throw new InvalidOperationException("Could not load universe details");
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error loading universe details: {ex.Message}");
                throw;
            }
        }

        private async Task LoadSubplacesAsync(long universeId)
        {
            const string LOG_IDENT = "GameInformationViewModel::LoadSubplaces";

            try
            {
                if (universeId == 0)
                {
                    Subplaces.Clear();
                    HasSubplaces = false;
                    return;
                }

                using var client = new HttpClient();
                string url = $"https://develop.roblox.com/v1/universes/{universeId}/places?isUniverseCreation=false&limit=50&sortOrder=Asc";

                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch subplaces: {response.StatusCode}");
                    return;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var subplacesResponse = JsonSerializer.Deserialize<SubplacesResponse>(responseContent);

                if (subplacesResponse?.Data == null || !subplacesResponse.Data.Any())
                {
                    Subplaces.Clear();
                    HasSubplaces = false;
                    return;
                }

                var subplacesList = new List<PlaceInfo>();

                foreach (var place in subplacesResponse.Data)
                {
                    string thumbnailUrl = await GetPlaceThumbnailUrlAsync(place.Id);
                    subplacesList.Add(new PlaceInfo(place.Id, place.UniverseId, place.Name, thumbnailUrl));
                }

                Subplaces = new ObservableCollection<PlaceInfo>(subplacesList);
                HasSubplaces = Subplaces.Any();
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Error loading subplaces: {ex.Message}");
                Subplaces.Clear();
                HasSubplaces = false;
            }
        }

        private async Task<Bitmap?> LoadBitmapFromUrlAsync(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return null;

            try
            {
                using var client = new HttpClient();
                var imageData = await client.GetByteArrayAsync(imageUrl);

                return await Task.Run(() =>
                {
                    try
                    {
                        using var stream = new MemoryStream(imageData);
                        return new Bitmap(stream);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                });
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task<string> GetPlaceThumbnailUrlAsync(long placeId)
        {
            try
            {
                var thumbnailResponse = await Http.GetJson<ApiArrayResponse<ThumbnailResponse>>(
                    $"https://thumbnails.roblox.com/v1/places/gameicons?placeIds={placeId}&returnPolicy=PlaceHolder&size=128x128&format=Png&isCircular=false");

                var firstThumbnail = thumbnailResponse?.Data?.FirstOrDefault();
                return firstThumbnail?.ImageUrl ?? "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        private string GetGenreName(string? genre)
        {
            if (string.IsNullOrEmpty(genre))
                return "All";

            return genre switch
            {
                "All" => "All",
                "Building" => "Building",
                "Horror" => "Horror",
                "TownAndCity" => "Town and City",
                "Military" => "Military",
                "Comedy" => "Comedy",
                "Medieval" => "Medieval",
                "Adventure" => "Adventure",
                "SciFi" => "Sci-Fi",
                "Naval" => "Naval",
                "FPS" => "FPS",
                "RPG" => "RPG",
                "Sports" => "Sports",
                "Fighting" => "Fighting",
                "Western" => "Western",
                _ => genre
            };
        }

        [RelayCommand]
        private async Task CopyGameLink()
        {
            string gameUrl = $"https://www.roblox.com/games/{_placeId}";

            var topLevel = TopLevel.GetTopLevel(null);
            if (topLevel?.Clipboard != null)
            {
                await topLevel.Clipboard.SetTextAsync(gameUrl);

                Frontend.ShowMessageBox(
                    "Copied game link successfully.",
                    MessageBoxImage.Information
                );
            }
        }

        [RelayCommand]
        private async Task RefreshInformation() => await LoadGameInformationAsync();
    }
}