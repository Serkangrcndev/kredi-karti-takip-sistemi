namespace kredi_karti_takip;

public partial class Form1
{
    private DateOnly SelectedPeriod => PaymentSchedule.MonthOf(DateOnly.FromDateTime(month.Value));

    private (string Text, Color Color) PaymentText(Card card, DateOnly period, DateOnly today)
    {
        if (PaymentSchedule.IsPaid(ledger, card.Id, period)) return ("Ödendi", Green);
        var due = PaymentSchedule.DueDate(card, period);
        if (due is null) return ("Ödeme günü ayarlanmadı", Muted);
        return due < today ? ("Gecikti", Color.FromArgb(166, 44, 33)) : ("Bekliyor", Muted);
    }

    private async Task EditPayment()
    {
        var target = connection;
        var active = ActiveCards;
        if (target is null || active.Length == 0) return;
        var period = SelectedPeriod;
        var picker = CardPicker(active);
        var paid = new CheckBox { Text = "Bu dönemin ödemesi yapıldı", Height = 28 };
        var statusLabel = LabelFor("", 10, Ink, true);
        Card SelectedCard() => active.First(card => card.Id == ((Choice)picker.SelectedItem!).Id);
        void Populate()
        {
            var card = SelectedCard();
            bool isPaid = PaymentSchedule.IsPaid(ledger, card.Id, period);
            paid.Checked = isPaid;
            paid.Enabled = isPaid || PaymentSchedule.DueDate(card, period) is not null;
            var due = PaymentSchedule.DueDate(card, period);
            statusLabel.Text = due is not null ? $"{PaymentText(card, period, DateOnly.FromDateTime(DateTime.Today)).Text} · {due:dd.MM.yyyy}"
                : PaymentText(card, period, DateOnly.FromDateTime(DateTime.Today)).Text;
        }
        picker.SelectedIndexChanged += (_, _) => Populate();
        Populate();
        using var dialog = Editor("Ödeme durumu", $"{period.ToString("MMMM yyyy", Turkish)} ödeme dönemi",
            ("Kart", picker), ("Durum ve son ödeme günü", statusLabel), ("Ödeme kaydı", paid));
        timer.Stop();
        try
        {
            while (dialog.ShowDialog(this) == DialogResult.OK)
            {
                var card = SelectedCard();
                if (paid.Checked == PaymentSchedule.IsPaid(ledger, card.Id, period)) break;
                DateOnly? paidOn = paid.Checked ? DateOnly.FromDateTime(DateTime.Today) : null;
                if (await Exchange(target, new SetCardPayment(card.Id, period, paidOn))) break;
            }
        }
        finally { if (!IsDisposed) timer.Start(); }
    }

    private async Task RestoreBackup()
    {
        var target = connection;
        if (target is null) return;
        var folder = SharedLedger.BackupFolder(target.Path);
        timer.Stop();
        try
        {
            if (!await Task.Run(() => Directory.Exists(folder)))
            {
                MessageBox.Show(this, "Henüz erişilebilir bir yedek bulunamadı.", "Yedekler");
                return;
            }

            using var picker = new OpenFileDialog
            {
                Title = "Geri yüklenecek defter yedeğini seçin", Filter = "Defter yedekleri (*.json)|*.json",
                InitialDirectory = folder, CheckFileExists = true, RestoreDirectory = true
            };
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            if (!string.Equals(Path.GetDirectoryName(picker.FileName), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Ortak defterin Yedekler klasöründeki bir dosyayı seçin.", "Yedekler");
                return;
            }

            var answer = MessageBox.Show(this,
                $"“{Path.GetFileName(picker.FileName)}” ortak deftere geri yüklensin mi? Mevcut defter ayrıca yedeklenecek.",
                "Yedeği geri yükle", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer == DialogResult.Yes && await Exchange(target, new RestoreLedger(picker.FileName)))
                MessageBox.Show(this, "Yedek geri yüklendi.", "Yedekler");
        }
        finally { if (!IsDisposed) timer.Start(); }
    }
}
