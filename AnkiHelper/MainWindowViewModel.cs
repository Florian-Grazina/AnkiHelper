using AnkiHelper.Models;
using AnkiHelper.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnkiHelper
{
    internal partial class MainWindowViewModel : ObservableObject
    {
        #region fields
        private readonly AnkiConnectService _ankiService;
        #endregion

        #region constructor
        public MainWindowViewModel()
        {
            DeckNameCollection = [];
            _ankiService = new();
            newVocabsJson = GetPromptToCreateDico();
        }
        #endregion

        #region observable properties
        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<string> deckNameCollection;

        [ObservableProperty]
        private string? selectedDeckId;

        [ObservableProperty]
        private string newVocabsJson;
        #endregion

        #region commands
        [RelayCommand]
        private async Task AddCards()
        {
            IsBusy = true;
            IEnumerable<VocabDTO> vocabCollection = TryParseInputToVocabDTO();

            if (SelectedDeckId == null)
                return;

            foreach (VocabDTO vocab in vocabCollection)
                await _ankiService.AddCardAsync(SelectedDeckId, vocab);

            IsBusy = false;
        }

        [RelayCommand]
        private async Task RefreshDecks()
        {
            IsBusy = true;
            DeckNameCollection.Clear();
            try
            {
                foreach (string deckName in await _ankiService.GetDecksAsync())
                    DeckNameCollection.Add(deckName);
                SelectedDeckId = DeckNameCollection.FirstOrDefault();
                NewVocabsJson = GetPromptToCreateDico();
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task Reload()
        {
            ErrorMessage = string.Empty;
            IsBusy = true;
            await Task.Delay(100);
            IsBusy = false;
        }
        #endregion

        #region public methods
        public async Task OnLoad()
        {
            try
            {
                IsBusy = true;
                _ankiService.EnsureAnkiIsRunning();

                foreach (string deckName in await _ankiService.GetDecksAsync())
                    DeckNameCollection.Add(deckName);

                SelectedDeckId = DeckNameCollection.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region private methods
        private string GetPromptToCreateDico()
        {
            string prompt =
                $"""
                C# : Create me a collection of object of model:
                Lesson (string),
                Type (collection of enum)
                Japanese (string)
                English (string)

                Enum : Adj, Phra, N, V, Prep, Adv

                only respond with the collection, json format
                """;

            return prompt;
        }

        private IEnumerable<VocabDTO> TryParseInputToVocabDTO()
        {
            string json = NewVocabsJson.Trim().TrimEnd().TrimStart();

            IEnumerable<VocabDTO> vocabDtos = JsonConvert.DeserializeObject<IEnumerable<VocabDTO>>(json)
                ?? throw new InvalidCastException("Could not parse the input to the expected format.");

            return vocabDtos;
        }
        #endregion
    }
}
