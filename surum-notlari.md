Yenilikler:
- Aygıt bazlı sürücü arama: Windows Update'in yanında artık her aygıt için Microsoft Update Kataloğu'nda da daha yeni sürücü aranır
  (önce bilgisayar modelinize özel olanlar; grafik, ağ ve Bluetooth'ta Intel/NVIDIA/Realtek gibi çip üreticisinin kendi sürücüsü de).
  Paketin aygıtınızı gerçekten desteklediği indirmeden önce denetlenir; başka bir bilgisayar markasına ait paket kurulmaz.
- Sürücü yedeklerinden kurma: USB bellekteki, diğer disklerdeki ve "Sürücü Yedekleri" klasöründeki eksik ya da daha yeni sürücüler bulunup kurulur
- Listede her güncellemenin kaynağı (Windows Update, Microsoft Kataloğu, Yedek) gösterilir
- "Format Sonrası Kurulum" bu iki yeni kaynağı da kullanır

Düzeltmeler:
- Yönetici olarak yeniden başlatınca program, basılan işlemi (Format Sonrası Kurulum, Sürücüleri Yedekle) kaldığı yerden sürdürür
- Windows izin penceresinde vazgeçilirse artık bir uyarı gösterilir (önceden hiçbir şey olmuyordu)
- Belgeler klasörü OneDrive'daysa sürücü yedekleri artık OneDrive'a değil, eşitlenmeyen C:\Kullanıcılar\<ad>\Sürücü Yedekleri klasörüne alınır
- Düğmeler ekran okuyucular ve erişilebilirlik araçlarıyla kullanılabilir

What's new:
- Per-device driver search: besides Windows Update, every device is now also checked in the Microsoft Update Catalog
  (first drivers made for your PC model; for graphics, network and Bluetooth also the chip maker's own driver such as Intel/NVIDIA/Realtek).
  Each package is checked to support your device before downloading; packages for other PC brands are never installed.
- Install from driver backups: missing or newer drivers in backups on USB drives, other disks and the "Sürücü Yedekleri" folder are found and installed
- The list shows where each update comes from (Windows Update, Microsoft Catalog, Backup)
- "Post-Install Setup" uses both new sources

Fixes:
- After restarting as administrator, the program continues the action you chose (Post-Install Setup, Back Up Drivers)
- Declining the Windows permission prompt now shows a message (previously nothing happened)
- If Documents is in OneDrive, driver backups now go to the non-synced C:\Users\<name>\Sürücü Yedekleri folder instead of OneDrive
- Buttons work with screen readers and accessibility tools

Kurulum / Install: SurucuAraciKurulum.exe (yönetici yetkisi gerekmez / no admin rights needed)
