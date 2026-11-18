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
        }
        #endregion

        #region observable properties
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCardsCommand))]
        [NotifyCanExecuteChangedFor(nameof(OpenAnkiCommand))]
        [NotifyCanExecuteChangedFor(nameof(RefreshDecksCommand))]
        [NotifyCanExecuteChangedFor(nameof(FixTagsCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteAdjTagCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddCssCommand))]
        private bool isBusy;

        [ObservableProperty]
        private string? errorMessage;

        [ObservableProperty]
        private ObservableCollection<string> deckNameCollection;

        [ObservableProperty]
        private string? selectedDeckId;

        [ObservableProperty]
        private string newVocabsJson = string.Empty;
        #endregion

        #region commands
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task AddCards()
        {
            try
            {
                IsBusy = true;
                IEnumerable<VocabDTO> vocabCollection = TryParseInputToVocabDTO();

                // Captured once: the selection must not change mid-import.
                string? deckId = SelectedDeckId;
                if (deckId == null)
                    return;

                foreach (VocabDTO vocab in vocabCollection)
                {
                    await _ankiService.AddCardAsync(deckId, vocab);
                    await Task.Delay(25);
                }

                NewVocabsJson = string.Empty;
                await RefreshDecks();
                ErrorMessage = string.Empty;
            }
            catch(Exception ex)
            {
                ErrorMessage = ex.Message; 
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private void OpenAnki()
        {
            _ankiService.EnsureAnkiIsRunning();
        }

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task RefreshDecks()
        {
            IsBusy = true;
            try
            {
                string? selectedDeckId = SelectedDeckId;
                DeckNameCollection.Clear();

                foreach (string deckName in await _ankiService.GetDecksAsync())
                    DeckNameCollection.Add(deckName);

                SelectedDeckId = DeckNameCollection.FirstOrDefault(dn => dn == selectedDeckId);
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

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task FixTags()
        {
            try
            {
                IsBusy = true;
                if (SelectedDeckId == null)
                    return;
                await _ankiService.FixLessonTagsAsync(SelectedDeckId);
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

        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task DeleteAdjTag()
        {
            try
            {
                IsBusy = true;
                if (SelectedDeckId == null)
                    return;
                await _ankiService.DeleteAdjTag(SelectedDeckId);
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
        [RelayCommand(CanExecute = nameof(CanRunCommand))]
        private async Task AddCss()
        {
            try
            {
                IsBusy = true;
                if (SelectedDeckId == null)
                    return;
                await _ankiService.ApplyModernCssToDeckAsync(SelectedDeckId);
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
        // Commands are disabled while another one runs (covers keyboard and access keys, not just the mouse).
        private bool CanRunCommand() => !IsBusy;

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
