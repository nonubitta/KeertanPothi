﻿using DBTest.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Rg.Plugins.Popup.Extensions;
using System.Collections.ObjectModel;
using System.Collections;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using KeertanPothi.model;
using Acr.UserDialogs;
using Plugin.SimpleAudioPlayer;

namespace KeertanPothi.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ShabadDetails : ContentPage
    {
        #region Properties

        private bool initializing = false;
        SQLiteAsyncConnection _con;
        public int ShabadId { get; set; }
        public int? SelectedVerseId { get; set; }
        public Verse SelectedVerse { get; set; }
        public bool ToolbarVisible { get; set; } = true;
        ObservableCollection<Verse> versesObs = new ObservableCollection<Verse>();
        public int PothiId { get; set; }
        public int AngNo { get; set; }
        List<PothiShabad> pothiShabadList;
        private NitnemBani _nitnemBani;
        double lastYPos = 0;
        ISimpleAudioPlayer AudioPlayer;
        ToolbarItem itemPlay = null;
        ToolbarItem itemBookmark = null;
        ToolbarItem itemShare = null;
        ToolbarItem itemShare2 = null;


        public enum RequestSource
        {
            Search,
            Random,
            Pothi,
            Set,
            Ang,
            Nitnem,
            History
        }

        public RequestSource RequestFrom { get; set; }

        #endregion

        #region Constructors 

        /// <summary>
        /// User coming from Shabad search.
        /// </summary>
        /// <param name="shabadID"></param>
        /// <param name="verseID"></param>
        public ShabadDetails(int shabadID, int? verseID = null, bool fromHistory = false)
        {
            this.BindingContext = new Theme();
            initializing = true;
            ShabadId = shabadID;
            SelectedVerseId = verseID;
            if (fromHistory)
                RequestFrom = RequestSource.History;
            else
                RequestFrom = RequestSource.Search;
            InitializeComponent();
            LoadShabad();
            initializing = false;
        }

        /// <summary>
        /// User coming from Random shabad
        /// </summary>
        public ShabadDetails()
        {
            this.BindingContext = new Theme();
            initializing = true;
            var rand = new Random();
            ShabadId = rand.Next(20, 5540);
            RequestFrom = RequestSource.Random;
            InitializeComponent();
            LoadShabad();
            initializing = false;
        }

        /// <summary>
        /// User coming from Pothi shabad list
        /// </summary>
        /// <param name="shabadID"></param>
        /// <param name="verseID"></param>
        /// <param name="pothiId"></param>
        public ShabadDetails(int shabadID, int verseID, int pothiId)
        {
            this.BindingContext = new Theme();
            initializing = true;
            ShabadId = shabadID;
            SelectedVerseId = verseID;
            PothiId = pothiId;
            RequestFrom = RequestSource.Pothi;
            InitializeComponent();
            LoadShabad();
            //addPothi.IsEnabled = false;
            initializing = false;
        }

        /// <summary>
        /// User coming from Ang Search
        /// </summary>
        /// <param name="angNo"></param>
        public ShabadDetails(int angNo)
        {
            this.BindingContext = new Theme();
            initializing = true;
            AngNo = angNo;
            RequestFrom = RequestSource.Ang;
            InitializeComponent();
            LoadShabad();
            initializing = false;
        }

        /// <summary>
        /// User coming from Nitnetm
        /// </summary>
        /// <param name="bani"></param>
        public ShabadDetails(NitnemBani bani)
        {
            this.BindingContext = new Theme();
            initializing = true;
            this._nitnemBani = bani;
            RequestFrom = RequestSource.Nitnem;
            InitializeComponent();
            LoadShabad();
            initializing = false;
        }


        #endregion

        #region General

        protected override void OnAppearing()
        {
            if (RequestFrom != RequestSource.Ang && RequestFrom != RequestSource.Nitnem && RequestFrom != RequestSource.History)
                Queries.SaveShabadToHistory(ShabadId, SelectedVerseId);

            base.OnAppearing();
        }

        private void CreateToolbars()
        {
            ToolbarItems.Clear();
            if (RequestFrom == RequestSource.Nitnem)
            {
                if (_nitnemBani.Bookmark)
                {
                    CreateBookmarkToolbar();
                }
                AudioPlayer = CrossSimpleAudioPlayer.Current;
                try
                {
                    AudioPlayer.Load(_nitnemBani.BaniId + ".mp3");
                    CreatePlayToolbar();
                }
                catch { }
            }
            else
                CreateShareToolbar();

        }

        private void CreateBookmarkToolbar()
        {
            itemBookmark = new ToolbarItem();
            itemBookmark.Text = "Bookmark";
            itemBookmark.IconImageSource = ImageSource.FromResource("KeertanPothi.images.bookmark.png");
            itemBookmark.Order = ToolbarItemOrder.Primary;
            itemBookmark.Clicked += Bookmark_Clicked;
            ToolbarItems.Add(itemBookmark);
        }

        private void CreatePlayToolbar()
        {
            itemPlay = new ToolbarItem();
            itemPlay.Text = "Play";
            itemPlay.IconImageSource = ImageSource.FromResource("KeertanPothi.images.Play.png");
            itemPlay.Order = ToolbarItemOrder.Primary;
            itemPlay.Clicked += Play_Clicked;
            ToolbarItems.Add(itemPlay);
        }

        private void CreateShareToolbar()
        {
            itemShare = new ToolbarItem();
            itemShare.Text = "Share as Text";
            //itemShare.IconImageSource = ImageSource.FromResource("KeertanPothi.images.Share.png");
            itemShare.Order = ToolbarItemOrder.Secondary;
            itemShare.Clicked += share_Clicked;
            ToolbarItems.Add(itemShare);

            itemShare2 = new ToolbarItem();
            itemShare2.Text = "Share as File";
            //itemShare.IconImageSource = ImageSource.FromResource("KeertanPothi.images.Share.png");
            itemShare2.Order = ToolbarItemOrder.Secondary;
            itemShare2.Clicked += shareFile_Clicked;
            ToolbarItems.Add(itemShare2);
        }

        protected override void OnDisappearing()
        {
            AudioPlayer?.Stop();
            base.OnDisappearing();
        }

        private async void LoadShabad()
        {
            using (UserDialogs.Instance.Loading("Loading Shabad..."))
            {
                CreateToolbars();
                _con = DependencyService.Get<ISqliteDb>().GetSQLiteConnection();
                List<Verse> verses;
                if (RequestFrom == RequestSource.Ang)
                {
                    verses = await _con.QueryAsync<Verse>(Queries.VerseByAng(AngNo));
                }
                else if (RequestFrom == RequestSource.Nitnem)
                {
                    string qr = Queries.NitnemBaniShabads(_nitnemBani.BaniId);
                    verses = await _con.QueryAsync<Verse>(qr);
                }
                else
                {
                    verses = await _con.QueryAsync<Verse>(Queries.ShabadById(ShabadId));
                }
                versesObs = new ObservableCollection<Verse>(verses);
                if (versesObs.Count > 0)
                {
                    AddVishram();
                    ShowLadivaar();
                    lstShabad.ItemsSource = versesObs;
                    Verse verse = verses.FirstOrDefault(a => a.WriterID != null);
                    if (verse != null)
                        LoadShabadDetails(verses[0], verse?.WriterEnglish);
                    if (SelectedVerseId != null && SelectedVerseId > 0)
                    {
                        SelectedVerse = versesObs.FirstOrDefault(a => a.ID == SelectedVerseId);
                        lstShabad.ScrollTo(SelectedVerse, ScrollToPosition.MakeVisible, false);
                        SelectedVerse.ListBgColor = SelectedVerse?.PageBgTheme.DefaultItemBg;
                    }
                    else
                        lstShabad.ScrollTo(versesObs[0], ScrollToPosition.MakeVisible, false);
                }
                else
                    Util.ShowRoast("Error loading shabad...");
            }

            //if (RequestFrom == RequestSource.Nitnem)
            //{
            //    LoadMoreShabads();
            //}
        }

        private void LoadShabadDetails(Verse verse, string writerEnglish)
        {
            lblScripture.Text = "Scripture: " + verse.SourceEnglish;
            lblAng.Text = "Ang: " + verse.PageNo.ToString();
            lblWriter.Text = "Writer: " + writerEnglish;
            lblRaag.Text = "Raag: " + verse.RaagEnglish;
            if (RequestFrom == RequestSource.Ang)
                Title = "Ang: " + verse.PageNo.ToString();
            else if (RequestFrom == RequestSource.Nitnem)
                Title = _nitnemBani.EnglishName;
            //else if (RequestFrom == RequestSource.Pothi)
            //    Title = Pothi
            else
                Title = writerEnglish;
        }

        private async void LoadMoreShabads()
        {
            string[] shabadList = _nitnemBani.ShabadList.Split(',');
            Verse breaker = new Verse();
            breaker.Gurmukhi = "***************";
            breaker.GurmukhiHtml = "<div style='text-align:center; '>***************</div>";
            for (int i = 1; i < shabadList.Count(); i++)
            {
                int shabadId = 0;
                if (int.TryParse(shabadList[i], out shabadId))
                {
                    List<Verse> verses = await _con.QueryAsync<Verse>(Queries.ShabadById(shabadId));
                    if (Util.PrefShowVishraam)
                        verses = AddVishram(verses);
                    if (!_nitnemBani.IsSingleBani)
                        verses.Insert(0, breaker);
                    foreach (Verse verse in verses)
                        versesObs.Add(verse);
                }
            }
        }

        private void SettingChanged(Util.SettingName settingName, bool? value)
        {
            switch (settingName)
            {
                case Util.SettingName.GurmukhiVisible:
                    versesObs.ToList().ForEach(a => a.GurmukhiVisible = value.Value);
                    break;

                case Util.SettingName.EngTranslationVisible:
                    versesObs.ToList().ForEach(a => a.TranslationVisible = value.Value);
                    break;

                case Util.SettingName.PunTranslationVisible:
                    versesObs.ToList().ForEach(a => a.PunjabiTranslationVisible = value.Value);
                    break;

                case Util.SettingName.TransliterationVisible:
                    versesObs.ToList().ForEach(a => a.TransliterationVisible = value.Value);
                    break;

                case Util.SettingName.GurmukhiFontSize:
                    if (value.Value)
                        versesObs.ToList().ForEach(a => a.GurmukhiFontSize += 2);
                    else
                    {
                        if (versesObs[0].GurmukhiFontSize >= 8)
                            versesObs.ToList().ForEach(a => a.GurmukhiFontSize -= 2);
                    }
                    break;

                case Util.SettingName.EngTranslationFontSize:
                    if (value.Value)
                        versesObs.ToList().ForEach(a => a.EnglishFontSize += 2);
                    else
                    {
                        if (versesObs[0].EnglishFontSize >= 8)
                            versesObs.ToList().ForEach(a => a.EnglishFontSize -= 2);
                    }
                    break;

                case Util.SettingName.PunTranslationFontSize:
                    if (value.Value)
                        versesObs.ToList().ForEach(a => a.PunjabiFontSize += 2);
                    else
                    {
                        if (versesObs[0].PunjabiFontSize >= 8)
                            versesObs.ToList().ForEach(a => a.PunjabiFontSize -= 2);
                    }
                    break;

                case Util.SettingName.TransliterationFontSize:
                    if (value.Value)
                        versesObs.ToList().ForEach(a => a.TransliterationFontSize += 2);
                    else
                    {
                        if (versesObs[0].TransliterationFontSize >= 8)
                            versesObs.ToList().ForEach(a => a.TransliterationFontSize -= 2);
                    }
                    break;

                case Util.SettingName.FullScreen:
                    FullScreen(value.Value);
                    break;

                case Util.SettingName.NormalFont:
                    string fontName = value.Value ? Util.PunjabiFontKey : Util.HandFontKey;
                    versesObs.ToList().ForEach(a => a.GurmukhiFontName = fontName);
                    break;

                case Util.SettingName.HandFont:
                    string fontName2 = value.Value ? Util.HandFontKey : Util.PunjabiFontKey;
                    versesObs.ToList().ForEach(a => a.GurmukhiFontName = fontName2);
                    break;

                case Util.SettingName.Ladivaar:
                    versesObs.ToList().ForEach(a => a.GurmukhiHtml = a.Gurmukhi);
                    VishraamLadivaar();
                    break;

                case Util.SettingName.Vishraam:
                    versesObs.ToList().ForEach(a => a.GurmukhiHtml = a.Gurmukhi);
                    VishraamLadivaar();
                    break;

                case Util.SettingName.Theme:
                    Theme theme = new Theme();
                    versesObs?.ToList().ForEach(a => a.PageBgTheme = theme);
                    BindingContext = theme;
                    if (SelectedVerseId != null && SelectedVerseId > 0)
                    {
                        SelectedVerse = versesObs.FirstOrDefault(a => a.ID == SelectedVerseId);
                        SelectedVerse.ListBgColor = SelectedVerse.PageBgTheme.DefaultItemBg;
                    }
                    break;
            }
        }

        private async void FullScreen(bool isFullscreen)
        {
            ToolbarVisible = !ToolbarVisible;

            if (isFullscreen)
            {
                NavigationPage.SetHasNavigationBar(this, false);
                await EditToolbar.TranslateTo(0, 50, 200, Easing.SinOut);
                await Task.Delay(50);
                EditToolbar.IsVisible = false;
                btnRestore.IsVisible = true;
                //await btnRestore.TranslateTo(0, 0, 500, Easing.SinOut);
            }
            else
            {
                btnRestore.IsVisible = false;
                NavigationPage.SetHasNavigationBar(this, true);
                EditToolbar.IsVisible = true;
                await EditToolbar.TranslateTo(0, 0, 200, Easing.SinOut);
                //await btnRestore.TranslateTo(0, 70, 200, Easing.SinOut);
                //await Task.Delay(200);
            }
        }

        private List<Verse> AddVishram(List<Verse> verses = null)
        {
            if (verses == null)
                verses = versesObs.ToList();
            bool vishraam = Util.PrefShowVishraam;
            if (vishraam)
            {
                foreach (Verse verse in verses)
                {
                    if (verse.Visraam != null && vishraam)
                    {
                        JObject vishram = JObject.Parse(verse.Visraam);
                        string vishraamSource = GetVishraamSource(vishram);
                        if (!string.IsNullOrWhiteSpace(vishraamSource))
                        {
                            string[] line = verse.GurmukhiHtml.Split(' ');
                            int children = vishram[vishraamSource].Children().Count();
                            for (int i = 0; i < children; i++)
                            {
                                string vishraamWord = (string)vishram[vishraamSource][i]["p"];
                                string vishraamType = (string)vishram[vishraamSource][i]["t"];
                                int wordIndex = 0;
                                if (int.TryParse(vishraamWord, out wordIndex))
                                {
                                    string vishraamColor = vishraamType == "v" ? Util.MainVishraamColor : Util.SecondVishraamColor;
                                    line[wordIndex] = $"<font color=\"{vishraamColor}\">{line[wordIndex]}</font>";
                                    verse.GurmukhiHtml = string.Join(" ", line);
                                }
                            }
                        }
                    }

                }
            }
            return verses;
        }

        private string GetVishraamSource(JObject vishram)
        {
            string vishraamSource = string.Empty;
            if (vishram[Util.VishraamSource].HasValues || vishram[Util.VishraamSource].Children().Count() > 0)
            {
                vishraamSource = Util.VishraamSource;
            }
            else if (vishram[Util.VishraamSource2].HasValues || vishram[Util.VishraamSource2].Children().Count() > 0)
            {
                vishraamSource = Util.VishraamSource2;
            }
            else if (vishram[Util.VishraamSource3].HasValues || vishram[Util.VishraamSource3].Children().Count() > 0)
            {
                vishraamSource = Util.VishraamSource3;
            }
            return vishraamSource;
        }

        private void ShowLadivaar()
        {
            bool ladivaar = Util.PrefShowLadivaar;
            if (ladivaar)
            {
                versesObs.ToList().ForEach(a => a.GurmukhiHtml = a.GurmukhiHtml.Replace(" ", ""));
                versesObs.ToList().ForEach(a => a.GurmukhiHtml = a.GurmukhiHtml.Replace("fontcolor", "font color"));
            }
        }

        private void VishraamLadivaar()
        {
            AddVishram();
            ShowLadivaar();
        }

        #endregion

        #region buttons and taps

        private void menu_Clicked(object sender, EventArgs e)
        {
            if (versesObs.Count > 0)
            {
                SettingsPopup settingsPopup = new SettingsPopup(versesObs[0]);
                settingsPopup.SettingChangedEvent += SettingChanged;
                Navigation.PushPopupAsync(settingsPopup);
            }
        }

        private void share_Clicked(object sender, EventArgs e)
        {
            string str = $"Shared from Keertan Pothi(Id:{ShabadId.ToString()})\r\n";
            foreach (Verse verse in versesObs)
            {
                str += verse.GurmukhiUni + "\r\n";
            }
            Util.ShareText(str);
            //Util.ShareFile(str, "Shabad.txt", "Share Shabad");
        }

        private void shareFile_Clicked(object sender, EventArgs e)
        {
            string str = $"<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\"/></head><body>Shared from Keertan Pothi(Id:{ShabadId.ToString()})\r\n";
            foreach (Verse verse in versesObs)
            {
                str += "<br>" + HtmlFileVishraam(verse) + "\r\n";
            }
            str += "<br></body></html>";
            Util.ShareFile(str, "Shabad.html", "Share Shabad");
        }

        private string HtmlFileVishraam(Verse verse)
        {
            bool vishraam = Util.PrefShowVishraam;
            if (vishraam)
            {
                    if (verse.Visraam != null && vishraam)
                    {
                        JObject vishram = JObject.Parse(verse.Visraam);
                        string vishraamSource = GetVishraamSource(vishram);
                        if (!string.IsNullOrWhiteSpace(vishraamSource))
                        {
                            string[] line = verse.GurmukhiUni.Split(' ');
                            int children = vishram[vishraamSource].Children().Count();
                            for (int i = 0; i < children; i++)
                            {
                                string vishraamWord = (string)vishram[vishraamSource][i]["p"];
                                string vishraamType = (string)vishram[vishraamSource][i]["t"];
                                int wordIndex = 0;
                                if (int.TryParse(vishraamWord, out wordIndex))
                                {
                                    string vishraamColor = vishraamType == "v" ? Util.MainVishraamColor : Util.SecondVishraamColor;
                                    line[wordIndex] = $"<font color=\"{vishraamColor}\">{line[wordIndex]}</font>";
                                    verse.GurmukhiUni = string.Join(" ", line);
                                }
                            }
                        }
                    }
            }
            return verse.GurmukhiUni;
        }

        private void addPothi_Clicked(object sender, EventArgs e)
        {
            if (RequestFrom == RequestSource.Nitnem)
            {
                Util.ShowRoast("Can't add Nitnem to keertan pothi");
                return;
            }
            int? vId = SelectedVerseId;
            if (!vId.HasValue || vId < 1)
            {
                vId = versesObs.FirstOrDefault(a => a.WriterID != null)?.ID ?? versesObs[0].ID;
            }
            Navigation.PushPopupAsync(new SharePopup(ShabadId, vId.Value));
        }

        void TapGestureRecognizer_Tapped(object sender, EventArgs e)
        {
            //NavigationPage.SetHasNavigationBar(this, !ToolbarVisible.Value);
            //ToolbarVisible = !ToolbarVisible.Value;
            //PrevNext.IsVisible = ToolbarVisible.Value;
            //if (ToolbarVisible.Value)
            //{
            //    await PrevNext.TranslateTo(PrevNext.X, PrevNext.Y - 100, 1000, Easing.SinOut);
            //}
            //else
            //{
            //    await PrevNext.TranslateTo(PrevNext.X, PrevNext.Y + 100, 2000, Easing.SinIn);
            //}
        }

        private void PrevVerse_Clicked(object sender, EventArgs e)
        {
            LoadNextVerse(false);
        }

        private void NextVerse_Clicked(object sender, EventArgs e)
        {
            LoadNextVerse(true);
        }

        /// <summary>
        /// true for nextone, false for prior
        /// </summary>
        /// <param name="nxtOne"></param>
        private async void LoadNextVerse(bool nxtOne)
        {
            int newShabadId = 0;
            switch (RequestFrom)
            {
                case RequestSource.Nitnem:

                    List<NitnemBani> banis = await _con.QueryAsync<NitnemBani>(Queries.GetNextPrevBanis(_nitnemBani.BaniId, nxtOne));
                    if (banis != null && banis.Count > 0 && _nitnemBani != banis[0])
                    {
                        _nitnemBani = banis[0];
                        LoadShabad();
                    }
                    break;
                case RequestSource.Search:
                case RequestSource.Random:
                    if (nxtOne)
                        newShabadId = ShabadId + 1;
                    else
                        newShabadId = ShabadId - 1;
                    SelectedVerseId = null;
                    break;
                case RequestSource.Ang:
                    if (nxtOne)
                        AngNo += 1;
                    else
                        AngNo -= 1;
                    break;
                case RequestSource.Pothi:
                    if (pothiShabadList == null)
                        pothiShabadList = await _con.QueryAsync<PothiShabad>($"Select * from PothiShabad where PothiId = {PothiId} order by SortOrder");
                    int curInd = 0;
                    foreach (PothiShabad s in pothiShabadList)
                    {
                        if (s.ShabadId == ShabadId)
                        {
                            curInd = pothiShabadList.IndexOf(s);
                            break;
                        }
                    }

                    if (nxtOne)
                    {
                        if (curInd < pothiShabadList.Count - 1)
                        {
                            newShabadId = pothiShabadList[curInd + 1].ShabadId;
                            SelectedVerseId = pothiShabadList[curInd + 1].VerseId;
                        }
                    }
                    else
                    {
                        if (curInd > 0)
                        {
                            newShabadId = pothiShabadList[curInd - 1].ShabadId;
                            SelectedVerseId = pothiShabadList[curInd - 1].VerseId;
                        }
                    }
                    break;
                case RequestSource.Set:
                    break;
                default:
                    if (nxtOne)
                        newShabadId = ShabadId + 1;
                    else
                        newShabadId = ShabadId - 1;
                    break;
            }

            if ((newShabadId != 0 && newShabadId != ShabadId) || RequestFrom == RequestSource.Ang)
            {
                ShabadId = newShabadId;
                lstShabad.ScrollTo(versesObs[0], ScrollToPosition.Start, false);
                LoadShabad();
            }
            else
            {
                if (RequestFrom == RequestSource.Pothi)
                {
                    if (nxtOne)
                        Util.ShowRoast("This is the last shabad in the pothi");
                    else
                        Util.ShowRoast("This is the first shabad in the pothi");
                }
            }
        }

        private void btnMaximize_Clicked(object sender, EventArgs e)
        {
            FullScreen(true);
        }

        private void btnRestore_Clicked(object sender, EventArgs e)
        {
            FullScreen(false);
        }

        private void btnfindSimilar_Clicked(object sender, EventArgs e)
        {

        }

        private async void Back_Clicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync(true);
        }

        private void Play_Clicked(object sender, EventArgs e)
        {
            if (AudioPlayer.IsPlaying)
            {
                AudioPlayer.Pause();
                itemPlay.IconImageSource = ImageSource.FromResource("KeertanPothi.images.Play.png");
            }
            else
            {
                AudioPlayer.Play();
                itemPlay.IconImageSource = ImageSource.FromResource("KeertanPothi.images.Pause.png");
            }
        }

        private void Bookmark_Clicked(object sender, EventArgs e)
        {
            NitnemBookmarkPopup bookmarkPopup = new NitnemBookmarkPopup(_nitnemBani.BaniId);
            bookmarkPopup.BookmarkSelectedEvent += BookmarkSelected;
            Navigation.PushPopupAsync(bookmarkPopup);
        }

        private void BookmarkSelected(int verseId)
        {
            if (verseId > 0)
            {
                SelectedVerse = versesObs.FirstOrDefault(a => a.ID == verseId);
                lstShabad.ScrollTo(SelectedVerse, ScrollToPosition.MakeVisible, false);
            }
        }
    }
    #endregion
}

//private void PanGestureRecognizer_PanUpdated(object sender, PanUpdatedEventArgs e)
//{
//    if (e.StatusType == GestureStatus.Running || e.StatusType == GestureStatus.Completed)
//    {
//        double x = e.TotalX;
//        double y = e.TotalY;
//        versesObs.ToList().ForEach(a => a.GurmukhiFontSize += 2);
//    }
//}

//private void PinchGestureRecognizer_PinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
//{
//    if (e.Status == GestureStatus.Running || e.Status == GestureStatus.Completed)
//    {
//        double x = e.Scale;
//        Point y = e.ScaleOrigin;
//        versesObs.ToList().ForEach(a => a.GurmukhiFontSize -= 2);
//    }
//}
