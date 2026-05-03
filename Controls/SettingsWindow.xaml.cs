using System.Windows;
using Visunovia.Engine.Localization;

namespace Visunovia.Controls;

public partial class SettingsWindow : Window
{
    private string _selectedLanguage;

    public SettingsWindow()
    {
        InitializeComponent();
        _selectedLanguage = LocalizationService.Instance.CurrentLocale;
        LoadLanguages();
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var loc = LocalizationService.Instance;
        TitleText.Text = loc.GetString("Menu_EngineSettings");
        LanguageLabel.Text = "Language";
        OkButton.Content = loc.GetString("Dialog_Confirm");
        CancelButton.Content = loc.GetString("Dialog_Cancel");
    }

    private void LoadLanguages()
    {
        var cultures = LocalizationService.Instance.AvailableCultures;
        LanguageComboBox.Items.Clear();

        foreach (var culture in cultures)
        {
            var displayName = LocalizationService.Instance.GetLanguageDisplayName(culture.Name);
            LanguageComboBox.Items.Add(new LanguageItem { Code = culture.Name, DisplayName = displayName });
            if (culture.Name == _selectedLanguage)
            {
                LanguageComboBox.SelectedIndex = LanguageComboBox.Items.Count - 1;
            }
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is LanguageItem selected)
        {
            LocalizationService.Instance.LoadLocale(selected.Code);
            LocalizationService.Instance.SaveSettings();
        }
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private class LanguageItem
    {
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public override string ToString() => DisplayName;
    }
}
