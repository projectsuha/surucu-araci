# winget paketi

Bu klasördeki dosyalar Sürücü Aracı'nı `winget install projectsuha.SurucuAraci` ile kurulabilir yapar.
Microsoft'un [winget-pkgs](https://github.com/microsoft/winget-pkgs) deposuna gönderilmek için hazırdır.

## Göndermeden önce

- Depo **herkese açık** olmalı (winget kurulum dosyasını `InstallerUrl` adresinden şifresiz indirir).
- `InstallerSha256`, Releases sayfasındaki `SurucuAraciKurulum.exe` dosyasıyla aynı olmalı.
  Yeni sürümde yeniden hesaplayın: PowerShell'de `(Get-FileHash SurucuAraciKurulum.exe).Hash`

## Denetleme (Windows'ta)

```powershell
winget validate --manifest winget\manifests\p\projectsuha\SurucuAraci\2.8
winget settings --enable LocalManifestFiles   # yönetici olarak, bir kez
winget install --manifest winget\manifests\p\projectsuha\SurucuAraci\2.8
winget uninstall projectsuha.SurucuAraci
```

## Gönderme

1. GitHub'da `microsoft/winget-pkgs` deposunu **Fork**'layın.
2. Fork'ta `manifests/p/projectsuha/SurucuAraci/2.8/` klasörünü oluşturup bu klasördeki dört `.yaml` dosyasını yükleyin.
3. **Pull request** açın; ilk gönderide Microsoft'un katkı sözleşmesini (CLA) bot üzerinden kabul edin.
4. Otomatik denetimler geçince bir Microsoft görevlisi birleştirir (genellikle birkaç gün).

Alternatif: Windows'ta `winget install wingetcreate` kurup
`wingetcreate submit winget\manifests\p\projectsuha\SurucuAraci\2.8 --token <GitHub token>` ile tek komutla gönderebilirsiniz.

Sonraki sürümler için: `wingetcreate update projectsuha.SurucuAraci --version 2.8 --urls <yeni InstallerUrl> --submit`
