using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.Json;

namespace kredi_karti_takip;

public partial class Form1 : Form
{
    private const string Organization = "Erpiliç / Hasmet Gıda";
    private static readonly Color Ink = Color.FromArgb(27, 42, 35);
    private static readonly Color Muted = Color.FromArgb(79, 99, 88);
    private static readonly Color Green = Color.FromArgb(21, 91, 67);
    private static readonly Color Canvas = Color.FromArgb(244, 247, 243);
    private static readonly Color Line = Color.FromArgb(218, 228, 220);
    private static readonly Color Selected = Color.FromArgb(226, 241, 233);
    private static readonly Color Gold = Color.FromArgb(250, 220, 144);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly Label status = LabelFor("Defter seçilmedi", 10, Muted);
    private readonly Label total = LabelFor("₺0,00", 27, Color.White, true);
    private readonly Label detail = LabelFor("0 harcama", 10, Color.FromArgb(227, 242, 234));
    private readonly FlowLayoutPanel cards = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = false };
    private readonly ComboBox filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly CheckBox showDeletedCards = new() { Text = "Silinen kartları göster", AutoSize = true, ForeColor = Ink };
    private readonly DateTimePicker month = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MM / yyyy", ShowUpDown = true, Width = 106 };
    private readonly DataGridView grid = new();
    private readonly Button paymentStatus = MakeButton("Ödeme durumu", false, 130);
    private readonly Button editExpense = MakeButton("Düzenle", false, 90);
    private readonly Button backups = MakeButton("Yedekler", false, 92);
    private readonly Button addExpense = MakeButton("+ Harcama ekle", true, 146);
    private readonly Button editCard = MakeButton("Kartı düzenle", false, 122);
    private readonly Button deleteCard = MakeButton("Kartı sil", false, 94);
    private readonly Button addCard = MakeButton("+ Kart ekle");
    private readonly Button settings = MakeButton("Ortak klasör", false, 115);
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    private Connection? connection;
    private Ledger ledger = new(-1, [], []);
    private Card[] ActiveCards => ledger.Cards.Where(card => !card.Deleted).ToArray();
    private bool busy;
    private bool updatingFilters;
    private bool connected;
    private DateOnly renderedDay;
    private static readonly string ProfileFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KartDefteri");
    private readonly string profilePath = Path.Combine(ProfileFolder, "shared-folder.json");
    private readonly CardReminders reminders = new(Path.Combine(ProfileFolder, "reminders.json"));

    private sealed record Connection(string Folder)
    {
        public string Path => System.IO.Path.Combine(Folder, "kart-defteri.json");
    }
    private sealed record Choice(Guid? Id, string Name)
    {
        public override string ToString() => Name;
    }

    public Form1(bool startInBackground, EventWaitHandle activation)
    {
        InitializeComponent();
        Text = $"Kart Defteri · {Organization}";
        Icon = BrandIcon;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1130, 790);
        MinimumSize = new Size(800, 630);
        ClientSize = new Size(1110, 735);
        BackColor = Canvas;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(27, 17, 27, 23), ColumnCount = 1, RowCount = 7
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 82, 130, 58, 143, 54 })
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        Controls.Add(layout);

        var heading = new Panel { Width = 340, Height = 69, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        Place(heading, CardLogo(), 0, 8);
        Place(heading, LabelFor(Organization, 11, Green, true), 61, 4);
        Place(heading, LabelFor("Kart Defteri", 24, Ink, true), 60, 23);
        layout.Controls.Add(Bar(heading, addExpense, settings, backups), 0, 0);
        var summary = new Panel { Dock = DockStyle.Fill, BackColor = Green, Margin = Padding.Empty };
        summary.Paint += PaintSummary;
        var summaryTitle = LabelFor("AYLIK KART HARCAMASI", 10, Gold, true);
        foreach (var label in new[] { summaryTitle, total, detail }) label.BackColor = Color.Transparent;
        Place(summary, summaryTitle, 24, 16);
        Place(summary, total, 21, 36);
        Place(summary, detail, 25, 94);
        layout.Controls.Add(summary, 0, 1);
        layout.Controls.Add(Bar(LabelFor("Kartlar", 16, Ink, true), addCard, editCard, deleteCard, paymentStatus), 0, 2);
        cards.Margin = Padding.Empty;
        layout.Controls.Add(cards, 0, 3);
        layout.Controls.Add(Bar(LabelFor("Harcama geçmişi", 16, Ink, true), month, filter, showDeletedCards, editExpense), 0, 4);

        grid.Dock = DockStyle.Fill;
        grid.Margin = Padding.Empty;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.ReadOnly = true;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Line;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 36;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(231, 239, 233), ForeColor = Ink, Font = new Font(Font, FontStyle.Bold),
            Padding = new Padding(8, 0, 6, 0)
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = Ink, SelectionBackColor = Selected,
            SelectionForeColor = Ink, Padding = new Padding(8, 5, 6, 5)
        };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 249);
        AddColumn("Tarih", 16);
        AddColumn("Kart", 20);
        AddColumn("Açıklama", 48, wrap: true);
        AddColumn("Tutar", 18);
        grid.Columns[^1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns[^1].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        layout.Controls.Add(grid, 0, 5);
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        status.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
        var credit = LabelFor("Bu sistem Serkan Gürcan tarafından yapılmıştır.", 9, Muted);
        credit.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        footer.Controls.Add(status, 0, 0);
        footer.Controls.Add(credit, 1, 0);
        layout.Controls.Add(footer, 0, 6);

        filter.SelectedIndexChanged += (_, _) => { if (!updatingFilters) Render(); };
        showDeletedCards.CheckedChanged += (_, _) => RefreshHistory();
        month.ValueChanged += (_, _) => Render();
        settings.Click += async (_, _) => await Configure();
        editCard.Click += async (_, _) => await EditCard();
        deleteCard.Click += async (_, _) => await DeleteCard();
        addCard.Click += async (_, _) => await AddCard();
        addExpense.Click += (_, _) => AddExpense();
        editExpense.Click += (_, _) => { if (grid.CurrentRow?.Tag is Expense expense) AddExpense(expense); };
        grid.SelectionChanged += (_, _) => UpdateControls();
        grid.CellDoubleClick += (_, args) =>
        {
            if (!busy && connected && args.RowIndex >= 0 && grid.Rows[args.RowIndex].Tag is Expense expense)
                AddExpense(expense);
        };
        paymentStatus.Click += async (_, _) => await EditPayment();
        backups.Click += async (_, _) => await RestoreBackup();
        InitializeBackgroundMode(startInBackground, activation);
        timer.Tick += async (_, _) => await RefreshLedger();
        FormClosed += (_, _) =>
        {
            timer.Dispose();
            CloseReminderAlarm();
        };
        Shown += async (_, _) =>
        {
            try
            {
                if (File.Exists(profilePath))
                    connection = JsonSerializer.Deserialize<Connection>(File.ReadAllBytes(profilePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            if (backgroundOnly) Hide();
            ConfigureStartup();
            timer.Start();
            if (connection is null)
            {
                if (!backgroundOnly) await Configure();
            }
            else await RefreshLedger();
        };
        UpdateControls();
        Render();
    }

    private static Label LabelFor(string text, float size, Color color, bool bold = false) => new()
    {
        Text = text, AutoSize = true, ForeColor = color,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    private static Button MakeButton(string text, bool primary = false, int width = 105)
    {
        var button = new Button
        {
            Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Green : Color.White, ForeColor = primary ? Color.White : Ink,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = primary ? Green : Line;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(28, 112, 82) : Selected;
        return button;
    }

    private static void PaintSummary(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel || panel.ClientSize.Width == 0 || panel.ClientSize.Height == 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var border = new Pen(Color.FromArgb(82, 250, 220, 144), 1.5f);
        using var accent = new SolidBrush(Color.FromArgb(22, 250, 220, 144));
        int right = panel.ClientSize.Width;
        e.Graphics.FillEllipse(accent, right - 208, -109, 266, 266);
        e.Graphics.DrawEllipse(border, right - 175, -108, 229, 229);
        e.Graphics.DrawEllipse(border, right - 145, -111, 222, 222);
    }

    private static void Place(Control parent, Control child, int x, int y)
    {
        child.Location = new Point(x, y);
        parent.Controls.Add(child);
    }

    private static Control Bar(Control title, params Control[] actions)
    {
        var bar = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, ColumnCount = 2, RowCount = 1 };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        title.Anchor = AnchorStyles.Left;
        var right = new FlowLayoutPanel
        {
            AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            Anchor = AnchorStyles.Right, Margin = Padding.Empty
        };
        foreach (var action in actions)
        {
            action.Margin = new Padding(7, action is Button ? 0 : 5, 0, 0);
            right.Controls.Add(action);
        }
        bar.Controls.Add(title, 0, 0);
        bar.Controls.Add(right, 1, 0);
        return bar;
    }

    private void AddColumn(string title, float weight, bool wrap = false)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = title, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = wrap ? DataGridViewTriState.True : DataGridViewTriState.False }
        });
    }

    private void SetLedger(Ledger next)
    {
        if (ledger.Version == next.Version) return;
        ledger = next;
        RefreshHistory();
    }

    private void RefreshHistory()
    {
        var previous = (filter.SelectedItem as Choice)?.Id;
        updatingFilters = true;
        filter.Items.Clear();
        filter.Items.Add(new Choice(null, "Tüm kartlar"));
        var usedCards = ledger.Expenses.Select(row => row.CardId).ToHashSet();
        foreach (var card in ledger.Cards.Where(card => !card.Deleted ||
            (showDeletedCards.Checked && usedCards.Contains(card.Id))))
            filter.Items.Add(new Choice(card.Id, HistoryCardName(card)));
        filter.SelectedItem = filter.Items.Cast<Choice>().FirstOrDefault(choice => choice.Id == previous)
            ?? filter.Items[0];
        updatingFilters = false;
        Render();
    }

    private static string Money(decimal cents) => (cents / 100).ToString("C2", Turkish);

    private void Render()
    {
        renderedDay = DateOnly.FromDateTime(DateTime.Today);
        var selected = (filter.SelectedItem as Choice)?.Id;
        var hiddenCards = showDeletedCards.Checked ? new HashSet<Guid>() :
            ledger.Cards.Where(card => card.Deleted).Select(card => card.Id).ToHashSet();
        var monthly = ledger.Expenses.Where(row => row.Date.Year == month.Value.Year &&
            row.Date.Month == month.Value.Month && !hiddenCards.Contains(row.CardId)).ToArray();
        var visible = monthly.Reverse().Where(row => selected is null || row.CardId == selected)
            .OrderByDescending(row => row.Date).ToArray();
        total.Text = Money(visible.Sum(row => (decimal)row.Cents));
        detail.Text = $"{visible.Length} harcama  •  {(filter.SelectedItem as Choice)?.Name ?? "Tüm kartlar"}";
        var totals = monthly.GroupBy(row => row.CardId).ToDictionary(group => group.Key, group => group.Sum(row => (decimal)row.Cents));
        cards.SuspendLayout();
        while (cards.Controls.Count > 0) cards.Controls[0].Dispose();
        var activeCards = ActiveCards;
        if (activeCards.Length == 0)
            cards.Controls.Add(LabelFor("Aktif kart yok.", 11, Muted));
        foreach (var card in activeCards)
        {
            var active = selected == card.Id;
            var tile = new Panel
            {
                Width = 242, Height = 116, BackColor = active ? Selected : Color.White,
                Margin = new Padding(0, 0, 10, 0), Cursor = Cursors.Hand
            };
            var name = LabelFor(card.Name, 11, Ink, true);
            var bank = LabelFor(string.IsNullOrWhiteSpace(card.Bank) ? "Kredi kartı" : card.Bank, 9, Muted);
            foreach (var label in new[] { name, bank })
            {
                label.AutoSize = false;
                label.Size = new Size(195, 21);
                label.AutoEllipsis = true;
            }
            tile.Paint += (_, e) =>
            {
                using var outline = new Pen(active ? Green : Line);
                e.Graphics.DrawRectangle(outline, 0, 0, tile.Width - 1, tile.Height - 1);
                e.Graphics.FillRectangle(Brushes.DarkSeaGreen, 1, 1, tile.Width - 2, 3);
            };
            Place(tile, name, 12, 10);
            Place(tile, bank, 12, 31);
            Place(tile, LabelFor(Money(totals.GetValueOrDefault(card.Id)), 17, Green, true), 10, 49);
            var payment = PaymentText(card, SelectedPeriod, renderedDay);
            Place(tile, LabelFor(payment.Text, 9, payment.Color, true), 12, 88);
            void SelectCard(object? sender, EventArgs args) =>
                filter.SelectedItem = filter.Items.Cast<Choice>().First(choice => choice.Id == (active ? null : card.Id));
            tile.Click += SelectCard;
            foreach (Control child in tile.Controls) child.Click += SelectCard;
            cards.Controls.Add(tile);
        }
        cards.ResumeLayout();
        grid.Rows.Clear();
        var names = ledger.Cards.ToDictionary(card => card.Id, HistoryCardName);
        foreach (var row in visible)
        {
            int index = grid.Rows.Add(row.Date.ToString("dd.MM.yyyy"), names.GetValueOrDefault(row.CardId, "—"), row.Note, Money(row.Cents));
            grid.Rows[index].Tag = row;
        }
        grid.ClearSelection();
    }

    private void UpdateControls()
    {
        settings.Enabled = !busy;
        addCard.Enabled = connected && !busy;
        bool hasActiveCards = ledger.Cards.Any(card => !card.Deleted);
        editCard.Enabled = connected && !busy && hasActiveCards;
        deleteCard.Enabled = editCard.Enabled;
        addExpense.Enabled = editCard.Enabled;
        paymentStatus.Enabled = editCard.Enabled;
        editExpense.Enabled = connected && !busy && grid.SelectedRows.Count > 0 && grid.CurrentRow?.Tag is Expense;
        backups.Enabled = !busy && connection is not null;
    }

    private async Task RefreshLedger()
    {
        if (busy || connection is null) return;
        await Exchange(connection);
    }

    private async Task<bool> Exchange(Connection target, object? entry = null, bool configuring = false)
    {
        if (busy) return false;
        busy = true;
        UpdateControls();
        try
        {
            var next = await Task.Run(() =>
            {
                if (configuring) return SharedLedger.Open(target.Path);
                return entry is null ? SharedLedger.Read(target.Path) : SharedLedger.Save(target.Path, entry);
            });
            if (IsDisposed) return false;
            if (configuring)
            {
                connection = target;
                ledger = ledger with { Version = -1 };
            }
            SetLedger(next);
            if (renderedDay != DateOnly.FromDateTime(DateTime.Today)) Render();
            connected = true;
            status.Text = "●  Ortak defter";
            status.ForeColor = Color.FromArgb(24, 115, 82);
            ShowDueReminders();
            return true;
        }
        catch (SharedLedger.Rejected ex)
        {
            if (!IsDisposed) MessageBox.Show(Form.ActiveForm ?? this, ex.Message, "Kayıt kaydedilemedi");
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Program.LogError(ex);
            if (IsDisposed) return false;
            string message = ex switch
            {
                JsonException or InvalidDataException =>
                    "Defter dosyası okunamadı. Dosya düzeltilene kadar değişiklikler kaydedilemez.",
                IOException or UnauthorizedAccessException =>
                    "Deftere erişilemedi. Ağ klasörünü ve yazma iznini kontrol edip tekrar deneyin.",
                _ => "İşlem tamamlanamadı. Tekrar deneyin."
            };
            if (!configuring)
            {
                connected = false;
                status.Text = "●  Defter güncellenemedi";
                status.ForeColor = Muted;
                ShowDueReminders();
            }
            if (configuring || entry is not null) MessageBox.Show(Form.ActiveForm ?? this, message, "Kart Defteri");
            return false;
        }
        finally
        {
            busy = false;
            if (!IsDisposed) UpdateControls();
        }
    }

    private async Task Configure()
    {
        timer.Stop();
        try
        {
            var folder = Field(connection?.Folder ?? "", 2048);
            var browse = MakeButton("Gözat…", false, 84);
            var picker = new Panel { Height = 34 };
            folder.Width = 329;
            browse.Margin = Padding.Empty;
            Place(picker, folder, 0, 3);
            Place(picker, browse, 336, 0);
            using var dialog = Editor("Ortak klasör", "Tüm bilgisayarlarda aynı paylaşılan klasörü seçin.",
                ("Klasör yolu", picker));
            browse.Click += (_, _) =>
            {
                using var browser = new FolderBrowserDialog { Description = "Ortak defter klasörünü seçin", SelectedPath = folder.Text };
                if (browser.ShowDialog(dialog) == DialogResult.OK) folder.Text = browser.SelectedPath;
            };
            while (dialog.ShowDialog(this) == DialogResult.OK)
            {
                if (!Path.IsPathFullyQualified(folder.Text.Trim()))
                {
                    MessageBox.Show(this, "Tam klasör yolunu girin.", "Ortak klasör");
                    continue;
                }
                var target = new Connection(folder.Text.Trim());
                if (!await Exchange(target, configuring: true)) continue;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                    File.WriteAllText(profilePath, JsonSerializer.Serialize(target));
                    ConfigureStartup();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show(this, "Klasör bu oturum için seçildi. Tercih bir sonraki açılış için kaydedilemedi.", "Kart Defteri");
                }
                break;
            }
        }
        finally { if (!IsDisposed) timer.Start(); }
    }

    private async Task AddCard()
    {
        var target = connection;
        if (target is null) return;
        var name = Field("", 60);
        var bank = Field("", 60);
        var statement = DayPicker();
        var payment = DayPicker();
        var id = Guid.NewGuid();
        using var dialog = Editor("Kart ekle", "Hatırlatma almak için ayın gününü seçin.",
            ("Kart adı", name), ("Banka (isteğe bağlı)", bank),
            ("Hesap kesim günü", statement), ("Son ödeme günü", payment));
        timer.Stop();
        try
        {
            while (dialog.ShowDialog(this) == DialogResult.OK)
            {
                if (await Exchange(target, new NewCard(id, name.Text, bank.Text, SelectedDay(statement), SelectedDay(payment)))) break;
            }
        }
        finally { if (!IsDisposed) timer.Start(); }
    }

    private static ComboBox DayPicker()
    {
        var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, MaxDropDownItems = 10 };
        picker.Items.Add("Belirtilmedi");
        foreach (int day in Enumerable.Range(1, 31)) picker.Items.Add(day);
        picker.SelectedIndex = 0;
        return picker;
    }

    private static int? SelectedDay(ComboBox picker) => picker.SelectedItem is int day ? day : null;

    private static string HistoryCardName(Card card) => card.Deleted ? $"{card.Name} (silindi)" : card.Name;

    private ComboBox CardPicker(Card[] items)
    {
        var picker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var item in items) picker.Items.Add(new Choice(item.Id, item.Name));
        picker.SelectedItem = picker.Items.Cast<Choice>().FirstOrDefault(item => item.Id == (filter.SelectedItem as Choice)?.Id)
            ?? picker.Items[0];
        return picker;
    }

    private async Task EditCard()
    {
        var target = connection;
        var active = ActiveCards;
        if (target is null || active.Length == 0) return;
        var card = CardPicker(active);
        var name = Field("", 60);
        var bank = Field("", 60);
        var statement = DayPicker();
        var payment = DayPicker();
        Card SelectedCard() => active.First(item => item.Id == ((Choice)card.SelectedItem!).Id);
        void PopulateFields()
        {
            var selected = SelectedCard();
            name.Text = selected.Name;
            bank.Text = selected.Bank;
            statement.SelectedIndex = selected.StatementDay ?? 0;
            payment.SelectedIndex = selected.PaymentDay ?? 0;
        }
        card.SelectedIndexChanged += (_, _) => PopulateFields();
        PopulateFields();
        using var dialog = Editor("Kartı düzenle", "Düzenlemek istediğiniz kartı seçin.",
            ("Kart", card), ("Kart adı", name), ("Banka (isteğe bağlı)", bank),
            ("Hesap kesim günü", statement), ("Son ödeme günü", payment));
        timer.Stop();
        try
        {
            while (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var edit = new UpdateCard(SelectedCard(), name.Text, bank.Text, SelectedDay(statement), SelectedDay(payment));
                if (await Exchange(target, edit)) break;
            }
        }
        finally { if (!IsDisposed) timer.Start(); }
    }

    private async Task DeleteCard()
    {
        var target = connection;
        var active = ActiveCards;
        if (target is null || active.Length == 0) return;
        var card = CardPicker(active);
        using var dialog = Editor("Kartı sil", "Kart aktif listeden çıkarılır. Harcama geçmişi saklanır.", ("Kart", card));
        timer.Stop();
        try
        {
            while (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var selected = (Choice)card.SelectedItem!;
                var answer = MessageBox.Show(this, $"“{selected.Name}” kartı silinsin mi?",
                    "Kartı sil", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer == DialogResult.Yes && await Exchange(target, new DeleteCard(selected.Id!.Value))) break;
            }
        }
        finally { if (!IsDisposed) timer.Start(); }
    }

    private void AddExpense(Expense? original = null)
    {
        var target = connection;
        var available = ExpenseCards(original);
        if (target is null || available.Length == 0) return;
        var card = CardPicker(available);
        if (original is not null)
            card.SelectedItem = card.Items.Cast<Choice>().FirstOrDefault(item => item.Id == original.CardId);
        var amount = new NumericUpDown
        {
            Minimum = 0, Maximum = 999_999_999.99m, DecimalPlaces = 2, ThousandsSeparator = true,
            Value = original is null ? 0 : Math.Clamp(original.Cents / 100m, 0, 999_999_999.99m)
        };
        var date = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy", MinDate = new DateTime(1900, 1, 1) };
        if (original is not null)
        {
            var originalDate = original.Date.ToDateTime(TimeOnly.MinValue);
            date.Value = originalDate < date.MinDate ? date.MinDate : originalDate > date.MaxDate ? date.MaxDate : originalDate;
        }
        var note = Field(original?.Note ?? "", 240);
        var id = Guid.NewGuid();
        using var dialog = Editor(original is null ? "Harcama ekle" : "Harcama düzenle",
            "Kayıt, aynı klasörü kullanan bilgisayarlarda görünür.",
            ("Kart", card), ("Tutar (₺)", amount), ("Tarih", date), ("Açıklama", note));
        var save = (Button)dialog.AcceptButton!;
        save.DialogResult = DialogResult.None;
        bool saving = false;
        dialog.FormClosing += (_, args) => args.Cancel = saving;
        save.Click += async (_, _) =>
        {
            if (card.SelectedItem is not Choice { Id: Guid cardId })
            {
                MessageBox.Show(dialog, "Aktif bir kart seçin.", "Harcama ekle");
                return;
            }
            if (amount.Value <= 0 || string.IsNullOrWhiteSpace(note.Text))
            {
                MessageBox.Show(dialog, "Sıfırdan büyük bir tutar ve harcama açıklaması girin.", "Harcama ekle");
                return;
            }

            long cents = (long)(amount.Value * 100);
            var expenseDate = DateOnly.FromDateTime(date.Value);
            object expense = original is null
                ? new NewExpense(id, cardId, cents, expenseDate, note.Text)
                : new UpdateExpense(original, cardId, cents, expenseDate, note.Text);
            saving = true;
            save.Enabled = false;
            try
            {
                if (await Exchange(target, expense))
                {
                    saving = false;
                    dialog.DialogResult = DialogResult.OK;
                    return;
                }

                await RefreshLedger();
                var selected = (card.SelectedItem as Choice)?.Id;
                card.Items.Clear();
                foreach (var item in ExpenseCards(original)) card.Items.Add(new Choice(item.Id, HistoryCardName(item)));
                card.SelectedItem = card.Items.Cast<Choice>().FirstOrDefault(item => item.Id == selected);
            }
            finally
            {
                saving = false;
                if (!dialog.IsDisposed) save.Enabled = true;
            }
        };
        timer.Stop();
        try
        {
            if (dialog.ShowDialog(this) != DialogResult.OK || IsDisposed) return;
            month.Value = date.Value < month.MinDate ? month.MinDate : date.Value;
            if (filter.SelectedItem is Choice { Id: not null } && card.SelectedItem is Choice selected)
                filter.SelectedItem = filter.Items.Cast<Choice>().FirstOrDefault(item => item.Id == selected.Id)
                    ?? filter.Items[0];
        }
        finally { if (!IsDisposed) timer.Start(); }
    }

    private Card[] ExpenseCards(Expense? original) =>
        ledger.Cards.Where(card => !card.Deleted || card.Id == original?.CardId).ToArray();

    private static TextBox Field(string value, int maxLength) => new() { Text = value, MaxLength = maxLength };

    private static Form Editor(string title, string hint, params (string Caption, Control Input)[] fields)
    {
        var dialog = new Form
        {
            Text = title, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Canvas,
            Font = new Font("Segoe UI", 10), ClientSize = new Size(470, 153 + 60 * fields.Length)
        };
        Place(dialog, LabelFor(title, 20, Ink, true), 20, 14);
        var context = LabelFor(hint, 10, Muted);
        context.MaximumSize = new Size(420, 0);
        Place(dialog, context, 23, 56);
        int top = 97;
        foreach (var (caption, input) in fields)
        {
            Place(dialog, LabelFor(caption, 10, Ink, true), 22, top);
            input.Width = 420;
            input.AccessibleName = caption;
            Place(dialog, input, 25, top + 20);
            top += 60;
        }
        var save = MakeButton(title switch
        {
            "Ortak klasör" => "Klasörü kullan",
            "Kartı sil" => "Devam et",
            _ => "Kaydet"
        }, true);
        var cancel = MakeButton("Vazgeç");
        save.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        Place(dialog, cancel, 228, top + 3);
        Place(dialog, save, 340, top + 3);
        dialog.AcceptButton = save;
        dialog.CancelButton = cancel;
        return dialog;
    }
}


