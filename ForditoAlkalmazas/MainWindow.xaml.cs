using Azure.Storage.Blobs;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;


namespace ForditoAlkalmazas
{
    public partial class MainWindow : Window
    {
        private string selectedFilePath;

        // Azure Translator beállítás
        private string translatorKey = "";
        private string translatorEndpoint = "https://api.cognitive.microsofttranslator.com/";
        private string translatorRegion = "global";

        // Azure Blob Storage beállítás
        private string blobConnectionString = "";
        private string blobContainerName = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files (*.txt)|*.txt";
            if (openFileDialog.ShowDialog() == true)
            {
                selectedFilePath = openFileDialog.FileName;
                FilePathTextBox.Text = selectedFilePath;
                StatusTextBlock.Text = "Fájl kiválasztva.";
            }
        }


        private async void TranslateUpload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selectedFilePath))
            {
                MessageBox.Show("Kérlek válassz ki egy fájlt először!");
                return;
            }

            try
            {
                string text = File.ReadAllText(selectedFilePath);
                StatusTextBlock.Text = "Fordítás folyamatban...";

                // Célnyelv kiválasztása
                string targetLanguage = ((ComboBoxItem)LanguageComboBox.SelectedItem).Tag.ToString();

                string translatedText = await TranslateTextAsync(text, targetLanguage);

                StatusTextBlock.Text = "Fordítás kész, feltöltés Azure Blob-ra...";

                string fileName = Path.GetFileNameWithoutExtension(selectedFilePath) + $"_{targetLanguage}_translated.txt";
                await UploadToBlobAsync(translatedText, fileName);

                StatusTextBlock.Text = $"Fájl sikeresen feltöltve: {fileName}";
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Hiba történt: " + ex.Message;
            }
        }

        private async Task<string> TranslateTextAsync(string text, string targetLanguage)
        {
            using HttpClient client = new HttpClient();
            string route = $"/translate?api-version=3.0&to={targetLanguage}";
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", translatorKey);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region", translatorRegion);

            var body = new object[] { new { Text = text } };
            var requestBody = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(translatorEndpoint + route, requestBody);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(responseBody);
            return doc.RootElement[0].GetProperty("translations")[0].GetProperty("text").GetString();
        }

        private async Task UploadToBlobAsync(string content, string fileName)
        {
            BlobServiceClient blobServiceClient = new BlobServiceClient(blobConnectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);

            await containerClient.CreateIfNotExistsAsync();
            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            
            using MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await blobClient.UploadAsync(ms, overwrite: true);
        }
    }
}