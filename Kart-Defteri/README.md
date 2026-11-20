# Kart Defteri

**Kredi kartı harcamalarını, ödeme durumlarını ve hatırlatmaları tek bir ortak defterden yönetin.**

Kart Defteri, Windows için geliştirilmiş bir C# ve .NET 10 uygulamasıdır. Tek bilgisayarda yerel bir klasörle kullanabilir veya bir ağ klasörü üzerinden aynı defteri birden fazla bilgisayarla paylaşabilirsiniz.

## Özellikler

- **Kart ve harcama takibi** · Kartları ekleyin, düzenleyin veya aktif listeden kaldırın. Harcamaları karta ve aya göre filtreleyin, aylık toplamları görün ve hatalı kayıtları düzeltin.
- **Ödeme durumu** · Seçili ayda hangi kartların ödendiğini, hangilerinin beklediğini veya geciktiğini görün.
- **Sesli hatırlatmalar** · Hesap kesim gününde, son ödeme gününden üç gün önce ve son ödeme gününde uyarı alın. Ödendi olarak işaretlenen dönem için ödeme uyarıları durur.
- **Arka planda çalışma** · Pencere kapatıldığında uygulama Windows bildirim alanında çalışmayı sürdürür. EXE paketiyle kullanımda otomatik başlatmayı bildirim alanı menüsünden yönetebilirsiniz.
- **Yedekleme** · Uygulama, deftere yazılan günlerde tarihli bir yedek oluşturur. Önceki bir kopyayı **Yedekler** ekranından geri yükleyebilirsiniz.

## Hızlı başlangıç

Kaynak koddan çalıştırmak için Windows ve .NET 10 SDK gerekir. Depoyu klonladıktan sonra proje klasöründe PowerShell ile çalıştırın.

```powershell
dotnet run --project kredi_karti_takip.csproj
```

1. İlk açılışta defterin saklanacağı klasörü seçin. Tek bilgisayarda kullanım için boş bir yerel klasör yeterlidir.
2. İlk kartı ekleyin. Uygulama seçtiğiniz klasörde `kart-defteri.json` dosyasını oluşturur.
3. Kartın hesap kesim ve son ödeme günlerini girin, ardından harcamaları kaydetmeye başlayın.

### Ortak ağ klasörü

Tüm bilgisayarlarda aynı klasörü seçin. Örnek ağ yolu `\\OFIS-PC\Ortak\KartDefteri` şeklindedir. Kullanıcıların klasörde okuma, yazma ve değiştirme izni olmalı, paylaşımı sağlayan bilgisayar veya NAS açık kalmalıdır. Kart Defteri klasör seçimini hatırlar. **Ortak klasör** düğmesiyle seçim değiştirilebilir.

Ortak defteri açan tüm bilgisayarlarda aynı sürümü kullanın. Eski sürümler, ödeme durumu ve kart tarihleri gibi yeni alanları yazma sırasında kaybedebilir.

## Günlük kullanım

- **Kart silme** · Kart aktif listeden çıkar ve hatırlatmaları durur. Saklanan harcamalar **Silinen kartları göster** seçeneğiyle görülebilir.
- **Hatırlatmalar** · Ayda bulunmayan bir gün seçildiyse ayın son günü kullanılır. **Hatırlattım** düğmesi sesi durdurur ve aynı Windows kullanıcısı için o hatırlatmayı o gün kapatır. Sesli uyarı için oturum açık, bilgisayar uyanık ve uygulama çalışıyor olmalıdır.
- **Geri yükleme** · `Yedekler/` klasörü ortak defterin yanındadır. Uygulama geri yüklemeden önce mevcut defterin bir kopyasını da saklar.
- **Bildirim alanı** · Simgeye çift tıklayarak pencereyi açın veya sağ tık menüsünden **Çıkış** ile uygulamayı durdurun. EXE taşınırsa otomatik başlatma yolunu güncellemek için yeni konumdan bir kez açın.

## Windows paketi hazırlama

Aşağıdaki komut, .NET çalışma zamanını da içeren tek bir EXE üretir.

```powershell
dotnet publish kredi_karti_takip.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/Windows-x64
```

Çalıştırılabilir dosya `dist/Windows-x64/kredi_karti_takip.exe` konumunda oluşur. ARM64 için `win-arm64` ve `dist/Windows-arm64`, 32 bit Windows için `win-x86` ve `dist/Windows-x86` kullanın. Üretilen EXE dosyalarını dağıtmak için GitHub Releases’a ekleyebilirsiniz.

## Proje yapısı

| Yol | İçerik |
| --- | --- |
| [kredi_karti_takip.csproj](kredi_karti_takip.csproj) | Windows Forms masaüstü uygulaması |
| [SharedLedger.cs](SharedLedger.cs) | Ortak defter dosyası ve yedekleme |
| [Shared/Models.cs](Shared/Models.cs) | Kart, harcama ve ödeme kayıtlarının veri modeli |
| [Server/](Server/) | Önceki sunuculu sürümün ASP.NET Core API’si |

### Önceki sunuculu sürümden geçiş

Masaüstü uygulaması doğrudan ortak klasördeki defter dosyasıyla çalışır. Önceki sürümden geçerken, yeni kayıt eklemeden önce sunucudaki `ledger.json` dosyasının kopyasını ortak klasöre `kart-defteri.json` adıyla koyun.

Önceki API’yi Docker Compose ile çalıştırmak için [`.env.example`](.env.example) dosyasını `.env` adıyla kopyalayın. `LEDGER_DOMAIN` alanına Caddy için kullanılacak alan adını, `LEDGER_KEY` alanına en az 24 karakterlik gizli bir anahtar girin. Ardından `docker compose up -d --build` komutunu çalıştırın.
