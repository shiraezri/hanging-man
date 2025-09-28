using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace hanging_man;

public partial class MainPage : ContentPage
{
    private string currentWord = "";
    private Label[] letterLabels;
    private int wrongGuesses = 0;
    private int correctGuessesCount = 0;
    private int totalScore = 0;
    private static readonly Random rng = new();

    private const string PlayerNameKey = "PlayerName";
    private const string WordLengthKey = "WordLength";
    private const string TotalScoreKey = "TotalScore";

    private List<int> wordLengths = new List<int> { 4, 5, 6, 7, 8, 9 };
    public List<int> WordLengths
    {
        get => wordLengths;
        set
        {
            wordLengths = value;
            OnPropertyChanged(nameof(WordLengths));
        }
    }

    private int selectedWordLength = 7;
    public int SelectedWordLength
    {
        get => selectedWordLength;
        set
        {
            if (selectedWordLength != value)
            {
                selectedWordLength = value;
                OnPropertyChanged(nameof(SelectedWordLength));
                Preferences.Set(WordLengthKey, selectedWordLength);
                _ = StartNewGame();
            }
        }
    }

    private string hangmanImageSource = "man1.png";
    public string HangmanImageSource
    {
        get => hangmanImageSource;
        set
        {
            hangmanImageSource = value;
            OnPropertyChanged(nameof(HangmanImageSource));
        }
    }

    private string scoreText = "Score: 0";
    public string ScoreText
    {
        get => scoreText;
        set
        {
            scoreText = value;
            OnPropertyChanged(nameof(ScoreText));
        }
    }

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Load saved settings
        SelectedWordLength = Preferences.Get(WordLengthKey, 7);
        totalScore = Preferences.Get(TotalScoreKey, 0);
        PlayerNameEntry.Text = Preferences.Get(PlayerNameKey, "");

        CreateLetterButtons();
        _ = StartNewGame();
    }

    private void CreateLetterButtons()
    {
        LettersGrid.Children.Clear();
        char c = 'A';
        int row = 0, col = 0;
        int maxCols = LettersGrid.ColumnDefinitions.Count;

        while (c <= 'Z')
        {
            var btn = new Button
            {
                Text = c.ToString(),
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#CF7486"),
                TextColor = Color.FromArgb("#FFE6ED"),
                WidthRequest = 35,
                HeightRequest = 35,
                CornerRadius = 5,
                Margin = 2
            };
            btn.Clicked += OnLetterClick;
            LettersGrid.Add(btn, col, row);

            col++;
            if (col >= maxCols)
            {
                col = 0;
                row++;
            }
            c--;
        }
    }

    private async Task<List<string>> LoadWordsAsync(string filename)
    {
        using var fs = await FileSystem.OpenAppPackageFileAsync(filename);
        using var reader = new StreamReader(fs);
        var list = new List<string>();
        while (!reader.EndOfStream)
            list.Add(reader.ReadLine());
        return list;
    }

    private async Task StartNewGame()
    {
        wrongGuesses = 0;
        correctGuessesCount = 0;
        ScoreText = $"Score: {correctGuessesCount} | Total: {totalScore}";
        HangmanImageSource = "man1.png";

        foreach (var btn in LettersGrid.Children.OfType<Button>())
        {
            btn.IsEnabled = true;
        }

        var allWords = await LoadWordsAsync("dictionary.txt");
        List<string> filteredWords = allWords.Where(w => w.Trim().Length == selectedWordLength).ToList();
        currentWord = filteredWords[rng.Next(filteredWords.Count)].Trim().ToUpper();

        WordDisplay.Children.Clear();
        letterLabels = new Label[currentWord.Length];

        for (int i = 0; i < currentWord.Length; i++)
        {
            var lbl = new Label
            {
                Text = " ",
                FontSize = 28,
                WidthRequest = 40,
                HeightRequest = 40,
                BackgroundColor = Colors.Pink,
                TextColor = Color.FromArgb("#CF7486"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(2),
                FontAttributes = FontAttributes.Bold
            };
            letterLabels[i] = lbl;
            WordDisplay.Children.Add(lbl);
        }
    }

    private async void OnLetterClick(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        btn.IsEnabled = false;
        string letter = btn.Text;
        bool hit = false;
        int pointsGainedThisClick = 0;

        for (int i = 0; i < currentWord.Length; i++)
        {
            // רק אות שלא נחשפה עדיין תתחשב בנקודה
            if (currentWord[i].ToString() == letter && letterLabels[i].Text == " ")
            {
                letterLabels[i].Text = letter;
                hit = true;
                pointsGainedThisClick++;
                correctGuessesCount++;
            }
        }

        if (hit)
        {
            // עדכון ניקוד נוכחי והצטברתי
            totalScore += pointsGainedThisClick;
            Preferences.Set(TotalScoreKey, totalScore);

            ScoreText = $"Score: {correctGuessesCount} | Total: {totalScore}";
        }
        else
        {
            wrongGuesses++;
            int imageIndex = Math.Min(wrongGuesses + 1, 7);
            HangmanImageSource = $"man{imageIndex}.png";

            if (wrongGuesses >= 6)
            {
                await DisplayAlert("Game Over", $"The word was: {currentWord}", "Restart");
                await StartNewGame();
                return;
            }
        }

        if (letterLabels.All(l => l.Text != " "))
        {
            await DisplayAlert("You Won!", $"The word was: {currentWord}\nTotal Score: {totalScore}", "Play Again");
            await StartNewGame();
        }
    }

    private async void RestartGame(object sender, EventArgs e)
    {
        await StartNewGame();
    }

    private void OnPlayerNameChanged(object sender, EventArgs e)
    {
        if (sender is Entry entry)
        {
            Preferences.Set(PlayerNameKey, entry.Text?.Trim() ?? "");
        }
    }
}
