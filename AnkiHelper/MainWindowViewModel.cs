using AnkiHelper.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

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
            _ankiService = new ();
        }
        #endregion

        #region observable properties
        [ObservableProperty]
        private ObservableCollection<string> deckNameCollection;
        #endregion

        #region commands
        #endregion

        #region public methods
        public async Task OnLoad()
        {
            foreach (string deckName in await _ankiService.GetDecksAsync())
                DeckNameCollection.Add(deckName);
        }
        #endregion

        #region private methods
        #endregion
    }
}
