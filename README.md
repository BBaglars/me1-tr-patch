# Mass Effect 1 (Legendary Edition) Türkçe Yama

Mass Effect 1 Legendary Edition için **yalnızca altyazı/metin** Türkçe yamasıdır. Ses İngilizce kalır,
yazılar Türkçe olur. Yama oyunun **İngilizce (INT)** dil yuvasının metinlerini değiştirir; oyunu
İngilizce dilde başlatın.

- 58.682 metin, 523 paket (diyaloglar, Kodeks, Günlük, menüler, görev ve gezegen açıklamaları)
- Oyunun yazı tipi Ğ ğ İ ı Ş ş Ç ç Ö ö Ü ü harflerini zaten içerir, oyun içinde doğrulandı
- Orijinal dosyalar kurulumda yedeklenir ve geri yüklenebilir

## Kurulum (sürüm paketi)

1. Gereksinim: [.NET 10 çalışma zamanı](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet` komutu PATH'te olmalı), Windows PowerShell.
2. Zip'i bir klasöre açın. Oyun klasörünüz varsayılandan farklıysa üç betiğe de `-BioGame "<...>\Game\ME1\BioGame"` parametresini verin.
3. Oyunu kapatın, ardından:

       scripts\build-staging.ps1 -Json data\tr -Staging staging   # oyun dosyalarından yamalı kopyalar üretir
       scripts\install.ps1                                         # deneme (hiçbir şey yazmaz)
       scripts\install.ps1 -Apply                                  # yedek alır ve kurar

4. Geri almak için: `scripts\uninstall.ps1 -Apply`

Sürüm paketi oyunun kendi dosyalarını içermez; yamalı paketler sizin kendi kopyanızdan üretilir.

## Depo

- `data/en`, `data/tr`: İngilizce kaynak ve Türkçe çeviri (JSON, `{key, en, tr}`)
- `translation/`: sözlük, stil rehberi, QA betikleri, çeviri parçaları
- `src/Me1Tlk`: TLK dışa/içe aktarma aracı (LegendaryExplorerCore üzerine)
- `scripts/`: staging, kurulum, geri alma
- `docs/`: bulgular ve envanter. Derleme notları: `BUILD.md`

Terim kararları: Biçiciler (Reapers), Konsey, Kütle Rölesi, Prothean işaretçisi, hac yolculuğu, telkin,
Thorian sarmaşığı. Tam liste `translation/glossary.md`.

Bu bağımsız bir hayran çalışmasıdır; Mass Effect, EA/BioWare'e aittir.
