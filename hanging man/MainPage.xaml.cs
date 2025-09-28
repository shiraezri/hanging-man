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
    // 🟢 משתנים כלליים לניהול מצב המשחק
    private string currentWord = "";            // המילה הנוכחית שהשחקן צריך לנחש
    private Label[] letterLabels;               // מערך של תוויות (Labels) – מציגות את האותיות שנחשפו
    private int wrongGuesses = 0;               // מספר טעויות נוכחי
    private int correctGuessesCount = 0;        // סך כל האותיות שהשחקן גילה נכון בסבב הנוכחי
    private int totalScore = 0;                 // ניקוד מצטבר בין משחקים
    private static readonly Random rng = new(); // מחולל מספרים אקראיים לבחירת מילה

    // 🟢 מפתחות לשמירה בהעדפות (Preferences) – כמו קובץ הגדרות מקומי
    private const string PlayerNameKey = "PlayerName";
    private const string WordLengthKey = "WordLength";
    private const string TotalScoreKey = "TotalScore";

    // 🟢 רשימת אורכי המילים האפשריים
    private List<int> wordLengths = new List<int> { 4, 5, 6, 7, 8, 9 };
    public List<int> WordLengths
    {
        get => wordLengths;
        set
        {
            wordLengths = value;
            OnPropertyChanged(nameof(WordLengths)); // מעדכן את ה־UI שהנתון השתנה
        }
    }

    //האורך שנבחר על ידי המשתמש מתוך ה־Picker
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
                Preferences.Set(WordLengthKey, selectedWordLength); // שומר את הבחירה
                _ = StartNewGame(); // מתחיל משחק חדש עם האורך החדש
            }
        }
    }

    // 🟢 נתיב התמונה שמציגה את מצב האיש התלוי
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

    //  טקסט הניקוד שמוצג למשתמש
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

    //  בנאי הדף הראשי
    public MainPage()
    {
        InitializeComponent();
        BindingContext = this; // מאפשר ל־XAML לגשת למאפיינים הציבוריים

        // קריאה לערכים שנשמרו מהפעם הקודמת (אם קיימים)
        SelectedWordLength = Preferences.Get(WordLengthKey, 7);
        totalScore = Preferences.Get(TotalScoreKey, 0);
        PlayerNameEntry.Text = Preferences.Get(PlayerNameKey, "");

        CreateLetterButtons(); // יצירת כפתורי האותיות A–Z
        _ = StartNewGame();    // מתחילים משחק ראשון
    }

    //  יוצר את כפתורי האותיות ומסדר אותם בגריד
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
                Style = (Style)Resources["LetterButtonStyle"]
            };
            btn.Clicked += OnLetterClick; // חיבור לאירוע לחיצה על אות
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

    //  טוען את כל המילים מקובץ מילים (dictionary.txt) מתוך משאבי האפליקציה
    private async Task<List<string>> LoadWordsAsync(string filename)
    {
        using var fs = await FileSystem.OpenAppPackageFileAsync(filename);
        using var reader = new StreamReader(fs);
        var list = new List<string>();
        while (!reader.EndOfStream)
            list.Add(reader.ReadLine());
        return list;
    }

    //  התחלת משחק חדש: מאפסת נתונים, בוחרת מילה אקראית ומכינה את התצוגה
    private async Task StartNewGame()
    {
        wrongGuesses = 0;
        correctGuessesCount = 0;
        ScoreText = $"Score: {correctGuessesCount} | Total: {totalScore}";
        HangmanImageSource = "man1.png";

        // מאפשר את כל הכפתורים מחדש
        foreach (var btn in LettersGrid.Children.OfType<Button>())
            btn.IsEnabled = true;

        // בחירת מילה אקראית באורך שנבחר
        var allWords = await LoadWordsAsync("dictionary.txt");
        List<string> filteredWords = allWords.Where(w => w.Trim().Length == selectedWordLength).ToList();
        currentWord = filteredWords[rng.Next(filteredWords.Count)].Trim().ToUpper();

        // יצירת תוויות ריקות להצגת האותיות
        WordDisplay.Children.Clear();
        letterLabels = new Label[currentWord.Length];

        for (int i = 0; i < currentWord.Length; i++)
        {
            var lbl = new Label
            {
                Style = (Style)Resources["LetterLabelStyle"],
                Text = " " // רווח – כדי להראות ריק
            };
            letterLabels[i] = lbl;
            WordDisplay.Children.Add(lbl);
        }
    }

    //  לחיצה על אות – בדיקת פגיעה, עדכון ניקוד או העלאת שלב איש תלוי
    private async void OnLetterClick(object sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        btn.IsEnabled = false;  // מונע לחיצה חוזרת על אותה אות
        string letter = btn.Text;
        bool hit = false;       // האם נמצאה פגיעה במילה
        int pointsGainedThisClick = 0;

        // בדיקה אם האות קיימת במילה
        for (int i = 0; i < currentWord.Length; i++)
        {
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
            // אות נכונה – מוסיפים ניקוד
            totalScore += pointsGainedThisClick;
            Preferences.Set(TotalScoreKey, totalScore);
            ScoreText = $"Score: {correctGuessesCount} | Total: {totalScore}";
        }
        else
        {
            // אות שגויה – מגדילים את מספר הטעויות ומעדכנים תמונה
            wrongGuesses++;
            int imageIndex = Math.Min(wrongGuesses + 1, 7);
            HangmanImageSource = $"man{imageIndex}.png";

            // אם חרגנו ממספר הטעויות המותר – הפסד
            if (wrongGuesses >= 6)
            {
                string nickname = Preferences.Get(PlayerNameKey, null);
                string message = nickname is not null
                    ? $"Game Over, {nickname}!\nThe word was: {currentWord}"
                    : $"Game Over!\nThe word was: {currentWord}";

                await DisplayAlert("Game Over", message, "Restart");
                await StartNewGame();
                return;
            }
        }

        // בדיקה אם כל האותיות נחשפו → ניצחון
        if (letterLabels.All(l => l.Text != " "))
        {
            string nickname = Preferences.Get(PlayerNameKey, null);
            string message = nickname is not null
                ? $"You Won, {nickname}!\nThe word was: {currentWord}\nTotal Score: {totalScore}"
                : $"You Won!\nThe word was: {currentWord}\nTotal Score: {totalScore}";

            await DisplayAlert("You Won!", message, "Play Again");
            await StartNewGame();
        }
    }

    //  כפתור "Restart Game" – מתחיל משחק חדש מייד
    private async void RestartGame(object sender, EventArgs e)
    {
        await StartNewGame();
    }

    //  לא שומר כאן – השמירה נעשית רק אחרי בדיקת תקינות (Check_Input)
    private void OnPlayerNameChanged(object sender, EventArgs e)
    {
        // נשאר ריק כדי למנוע שמירה כפולה
    }

    //  בדיקת קלט של שם משתמש – רק אותיות אנגליות (A–Z) וספרות, חייב להתחיל באות
    private void Check_Input(object sender, TextChangedEventArgs e)
    {
        string nickname = e.NewTextValue ?? string.Empty;
        bool ok = true;

        // בדיקה שהתו הראשון הוא אות אנגלית
        if (nickname.Length == 0 || !IsEnglishLetter(nickname[0]))
            ok = false;

        // בדיקה שכל שאר התווים הם אותיות אנגליות או ספרות
        foreach (char c in nickname)
        {
            if (!IsEnglishLetter(c) && !char.IsDigit(c))
            {
                ok = false;
                break;
            }
        }

        if (ok)
        {
            // קלט תקין → מציג הודעת הצלחה ושומר את השם
            ErrorLabel.Text = $"Your Nick Name is: {nickname}";
            ErrorLabel.TextColor = Color.FromArgb("#CF7486");
            Preferences.Set(PlayerNameKey, nickname.Trim());
        }
        else
        {
            // קלט לא תקין → מציג הודעת שגיאה ולא שומר
            ErrorLabel.Text = "Invalid input! Use English letters (A–Z) and numbers only.";
            ErrorLabel.TextColor = Colors.Red;
            Preferences.Remove(PlayerNameKey); // מחיקה כדי לא להציג שם ישן
        }
    }

    private bool IsEnglishLetter(char c)
    {
        return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
    }
}
