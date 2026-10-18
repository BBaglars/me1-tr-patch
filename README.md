# Mass Effect 1 (Legendary Edition) Türkçe Yama

Mass Effect 1 Legendary Edition için **yalnızca altyazı/metin** Türkçe yamasıdır. Ses İngilizce kalır,
yazılar Türkçe olur. Yama oyunun **İngilizce (INT)** dil yuvasının metinlerini değiştirir; oyunu
İngilizce dilde başlatın.

- 58.682 metin, 523 paket (diyaloglar, Kodeks, Günlük, menüler, görev ve gezegen açıklamaları)
- Oyunun yazı tipi Ğ ğ İ ı Ş ş Ç ç Ö ö Ü ü harflerini zaten içerir, oyun içinde doğrulandı
- Orijinal dosyalar kurulumda yedeklenir ve geri yüklenebilir

## Kurulum

1. `ME1-TR-Yama.exe` dosyasını [Releases](../../releases) sayfasından indirip çalıştırın (ek kurulum gerekmez, .NET içinde).
2. Açılan pencerede oyunun klasörü otomatik bulunur. Bulunmazsa **Mass Effect Legendary Edition** klasörünü yazın veya **Gözat** ile seçin.
3. Oyunu kapatın, **Yamayı Kur / Güncelle** düğmesine basın. Orijinal dosyalar önce yedeklenir
   (`%LOCALAPPDATA%\ME1-TR-Yamaackup\original`).
4. Oyunu **İngilizce** dilde açın. Geri almak için **Orijinali Geri Yükle**.

Komut satırı: `ME1-TR-Yama.exe --install --game "<oyun klasörü>" [--backup "<yedek klasörü>"]`, `--uninstall` ile geri alma.

Geliştirme için `scripts\*.ps1` betikleri (aynı işi PowerShell ile yapar) ve `BUILD.md` duruyor. Exe: `dotnet publish -c Release src\Me1Patcher -o publish`.
Yamalı oyun dosyaları pakette yoktur; kurucu bunları sizin kendi oyun kopyanızdan üretir.

## Depo

- `data/en`, `data/tr`: İngilizce kaynak ve Türkçe çeviri (JSON, `{key, en, tr}`)
- `translation/`: sözlük, stil rehberi, QA betikleri, çeviri parçaları
- `src/Me1Tlk`: TLK dışa/içe aktarma aracı (LegendaryExplorerCore üzerine)
- `scripts/`: staging, kurulum, geri alma
- `docs/`: bulgular ve envanter. Derleme notları: `BUILD.md`

Terim kararları: Biçiciler (Reapers), Konsey, Kütle Rölesi, Prothean işaretçisi, hac yolculuğu, telkin,
Thorian sarmaşığı. Tam liste `translation/glossary.md`.

Bu bağımsız bir hayran çalışmasıdır; Mass Effect, EA/BioWare'e aittir.
