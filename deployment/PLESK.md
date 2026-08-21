# BORDER System - Plesk yayın planı

## Doğrulanan hedef mimari

- `panel.border.com.tr` Windows/IIS üzerinde bir ASP.NET Core root application olarak çalışır.
- Plesk, .NET 10.0.10 self-contained `win-x64`, `AspNetCoreModuleV2` ve in-process hosting probe'unu başarıyla çalıştırdı.
- Next.js frontend build makinesinde static export edilir; production sunucusunda Node.js gerekmez.
- `Border.Api.exe` API'yi ve `wwwroot` altındaki exported frontend'i aynı origin'de sunar.
- `/api/*` ve `/health` backend endpoint'leridir. Reverse proxy kullanılmaz.
- PostgreSQL/Npgsql korunur; veritabanı harici managed PostgreSQL servisinde barındırılır.

## Paketi oluşturma

Repository kökünden:

```powershell
.\deployment\build-plesk-package.ps1
```

Script frontend dependency/lint/build, backend restore/test/self-contained publish, static export kopyalama, ZIP ve SHA-256 adımlarını çalıştırır. Production paketi `deployment/output/border-system-plesk-win-x64.zip` olur. Probe projesi ve probe ZIP'i pakete girmez.

## Plesk environment variables

ASP.NET Core `.env` dosyasını otomatik okumaz. Aşağıdaki değerleri Plesk .NET application/process environment ekranında tanımlayın; gerçek secret'ları dosyaya veya frontend'e koymayın:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<managed PostgreSQL connection string>
Security__UseHttpsRedirection=false
Security__RequireSecureCookies=false
Security__UseHsts=false
ReverseProxy__Enabled=false
DataProtection__KeyPath=App_Data/DataProtectionKeys
Database__ApplyMigrationsOnStartup=false
Database__ForceTls12=false
SEED_DEMO_DATA=false
```

Same-origin browser istekleri için CORS kaydı gerekmez. Ayrı bir frontend origin'i kullanılmadıkça `Cors__AllowedOrigins__*` eklemeyin. `ASPNETCORE_URLS` IIS/ANCM tarafından yönetildiği için girilmemelidir.

Managed PostgreSQL connection string'i sağlayıcının CA/TLS talimatlarına göre, tercihen tam sertifika ve host doğrulamasıyla kurun. Firewall/allowlist için Plesk sunucusunun outbound IP bilgisini hosting sağlayıcısından doğrulayın.

`Database__ForceTls12` normalde `false` kalmalıdır. Yalnızca eski Windows/Schannel uyumluluğu nedeniyle TLS protokol pazarlığını sınırlamak gerektiğinde `true` yapın. Bu ayar Npgsql istemcisini TLS 1.2 ile sınırlar; connection string'deki `SSL Mode=VerifyFull` sertifika ve host doğrulamasını kapatmaz.

TLS 1.2 zorlamasına rağmen handshake hatası sürerse hosting desteğine [HOSTING-TLS-DIAGNOSTIC.md](HOSTING-TLS-DIAGNOSTIC.md) notunu iletin.

## Probe'dan production'a geçiş

1. Managed PostgreSQL yedeğini ve erişimini doğrulayın.
2. Migration'ları ayrı ve kontrollü bir adımda uygulayın. Normal production startup'ta `Database__ApplyMigrationsOnStartup=false` kalmalıdır.
3. Mevcut probe dosyalarını Plesk File Manager ile indirerek rollback yedeği alın.
4. `App_Data/DataProtectionKeys` klasörünü oluşturun ve Plesk application pool kullanıcısına modify/write izni verin.
5. Bu key klasörünü deployment sırasında silmeyin. Key dosyaları silinirse aktif session ve antiforgery verileri geçersiz olur.
6. Production ZIP'i `/panel.border.com.tr` document/application root'una, dosyalar doğrudan kökte olacak şekilde açın.
7. `web.config` ile `Border.Api.exe` aynı kökte olmalıdır. Startup alanı düzenlenebiliyorsa `Border.Api.exe` seçin.
8. Environment variable'ları girip uygulamayı yeniden başlatın.

Data Protection anahtarları dosya sisteminde kalıcıdır fakat Plesk'te doğrulanmış bir at-rest sertifika mekanizması varsayılmaz. Klasörü web'den indirilemez tutun, ACL'i yalnızca uygulama kimliğiyle sınırlayın ve yedekleyin.

## HTTP ve SSL geçişi

Geçici HTTP testi sırasında `UseHttpsRedirection`, `RequireSecureCookies` ve `UseHsts` false kalabilir. Gerçek personel hesabı kullanmayın; HTTP kimlik bilgilerini ve cookie'leri şifrelemez.

SSL kurulduktan sonra:

```text
Security__UseHttpsRedirection=true
Security__RequireSecureCookies=true
Security__UseHsts=true
```

Plesk sertifikası `panel.border.com.tr` adını kapsamalıdır. Kurumsal site URL'si de frontend build config'inde HTTPS'e alınmalıdır. API URL'si same-origin olduğu için frontend bu değişiklik nedeniyle yeniden build gerektirmez.

## Rollback

1. Uygulamayı Plesk'te durdurun veya `app_offline.htm` ile offline duruma alın.
2. `App_Data/DataProtectionKeys` klasörünü koruyun.
3. Bir önceki eksiksiz deployment yedeğini root'a geri yükleyin.
4. Veritabanı migration'larını otomatik geri almaya çalışmayın; ayrı veritabanı geri dönüş/yedek planını uygulayın.
5. Environment variable'ları önceki sürümle uyumlu hale getirip uygulamayı başlatın.

## Deployment sonrası smoke test

- `/`, `/login/`, `/dashboard/`, `/students/`, `/classes/` HTML döndürüyor.
- `/students/detail/?id=<guid>` ve `/classes/detail/?id=<guid>` açılıyor; geçersiz ID API isteği oluşturmuyor.
- `/health` PostgreSQL bağlantısıyla sağlıklı.
- Bilinmeyen `/api/...` 404 döndürüyor ve HTML fallback'e düşmüyor.
- Bilinmeyen UI yolu 404 HTML'i ve 404 status kodu döndürüyor.
- `/_next/static/*` asset'leri uzun immutable cache, HTML dosyaları `no-cache` ile sunuluyor.
- `/robots.txt` paneli indekslemeye kapatıyor.
- Login, CSRF, logout ve rol yetkileri beklenen 200/400/401/403 kodlarını döndürüyor.
- Recycle sonrasında Data Protection key dosyaları korunuyor ve mevcut session davranışı doğrulanıyor.
