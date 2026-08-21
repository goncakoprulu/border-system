# BORDER Studio Management

BORDER dans okulunun responsive yönetim sistemidir. Phase 1; kimlik doğrulama, rol tabanlı yetkilendirme, PostgreSQL domain modeli, audit altyapısı ve responsive uygulama kabuğunu kurar. Phase 2, production kalitesinde öğrenci ve veli yönetimini ekler. Ödeme, yoklama, üyelik ve raporlama iş akışları sonraki fazlara bırakılmıştır.

## Mimari

```text
apps/web                 Next.js App Router istemcisi
src/Border.Api           HTTP, auth, CORS, Swagger ve health endpoint'leri
src/Border.Application   API sözleşmeleri, roller/politikalar ve uygulama arayüzleri
src/Border.Domain        Framework bağımsız domain entity ve enum'ları
src/Border.Infrastructure EF Core, PostgreSQL, Identity, seed ve audit uygulaması
tests/Border.Tests       Model/constraint odaklı otomatik testler
```

Backend, EF entity'lerini API sözleşmesi olarak dışarı açmaz. Veritabanı ilişkilerinde tarihsel yoklama ve finans kayıtlarını korumak için destructive cascade kullanılmaz. Finansal alanlar `decimal(18,2)`, iş tarihleri PostgreSQL `date`, zaman damgaları UTC `timestamp with time zone` olarak modellenmiştir. Kullanıcı arayüzü gösterim zamanı `Europe/Istanbul` olarak kabul edilir.

## Gereksinimler

- .NET SDK 10 LTS
- Node.js 20+
- Docker Desktop / Docker Compose
- npm 10+

## İlk kurulum

```powershell
Copy-Item .env.example .env
Copy-Item apps/web/.env.example apps/web/.env.local
docker compose up -d postgres
dotnet tool restore
dotnet restore Border.slnx
npm ci --prefix apps/web
```

`.env` dosyası Docker Compose tarafından okunur. API için değişkenleri terminal oturumuna ayrıca tanımlayın. Bootstrap kullanıcısı yalnızca her iki bootstrap değişkeni açıkça verilirse oluşturulur:

```powershell
$env:ConnectionStrings__DefaultConnection='Host=localhost;Port=5433;Database=border;Username=border;Password=border_dev'
$env:Cors__AllowedOrigins__0='http://localhost:3000'
$env:BOOTSTRAP_ADMIN_EMAIL='admin@example.local'
$env:BOOTSTRAP_ADMIN_PASSWORD='Yerel-Güçlü-Parola-123!'
```

Örnekteki parolayı kişisel, güçlü bir yerel parola ile değiştirin. Gerçek/production parolasını repoya veya `.env.example` dosyasına yazmayın. İlk açılışta `Admin` ve `Management` rolleri bootstrap kullanıcıya atanır; roller (`Management`, `Instructor`, `Reception`, `Admin`) her açılışta idempotent biçimde seed edilir.

## Veritabanı ve migration

İlk migration repoda hazırdır. API açılışta bekleyen migration'ları uygular. Manuel komutlar:

```powershell
dotnet tool run dotnet-ef database update --project src/Border.Infrastructure --startup-project src/Border.Api
dotnet tool run dotnet-ef migrations add MigrationName --project src/Border.Infrastructure --startup-project src/Border.Api --output-dir Persistence/Migrations
```

`MembershipPriceHistory` üzerinde PostgreSQL `btree_gist` tabanlı exclusion constraint bulunur; aynı öğrenci üyeliğinde çakışan fiyat geçerlilik dönemleri veritabanı seviyesinde engellenir. Yoklamada `(LessonSessionId, StudentId)` benzersizdir.

## Uygulamaları çalıştırma

İki ayrı terminal kullanın:

```powershell
dotnet run --project src/Border.Api --launch-profile http
```

```powershell
npm run dev --prefix apps/web
```

VS Code'da `BORDER: Tümünü Başlat` görevi PostgreSQL, API ve web uygulamasını sırasıyla başlatır.

## Yerel adresler

- Web: http://localhost:3000
- API: http://localhost:5100
- Swagger: http://localhost:5100/swagger
- Health: http://localhost:5100/health

## Auth ve güvenlik

- ASP.NET Core Identity parola hashleme ve lockout uygular.
- Oturum, `HttpOnly` cookie ile tutulur; uzun ömürlü sırlar `localStorage` içine yazılmaz.
- State-changing auth çağrıları antiforgery token ile korunur.
- Local CORS yalnızca yapılandırılan origin'e credentials desteği verir.
- API'de authenticated, Management, Instructor ve Admin policy örnek endpoint'leri vardır.
- Audit altyapısı kullanıcı, eylem, entity, önceki/yeni JSON değerleri, UTC zaman ve IP alanlarını taşır. Parola/token audit verisine verilmemelidir.

## Student Management API

Phase 2 öğrenci modülü aşağıdaki endpoint'leri sağlar:

```text
GET    /api/students
GET    /api/students/{id}
POST   /api/students
PUT    /api/students/{id}
PATCH  /api/students/{id}/status
DELETE /api/students/{id}
GET    /api/students/{id}/guardians
POST   /api/students/{id}/guardians
PUT    /api/students/{id}/guardians/{guardianId}
DELETE /api/students/{id}/guardians/{guardianId}
```

Admin, Management ve Reception öğrenci modülüne erişebilir; Instructor genel öğrenci dizinine erişemez. Arşivleme ve arşivlenmiş kayıtları görüntüleme yalnızca Admin/Management rollerine açıktır.

Telefonlar ayrı bir uluslararası telefon kütüphanesi kullanılmadan basit biçimde normalize edilir: boşluk, parantez, nokta ve tire kaldırılır; rakamlar ve yalnızca baştaki `+` korunur. Bu değer arama ve olası duplicate uyarısı için kullanılır. Aynı normalize telefon/e-posta yeni kaydı engellemez, API uyarı döndürür.

## Kalite komutları

```powershell
dotnet build Border.slnx
dotnet test Border.slnx --no-build
npm run lint --prefix apps/web
npm run build --prefix apps/web
dotnet list Border.slnx package --vulnerable --include-transitive
```

## Phase 3 ve sonrasına bırakılanlar

- Eğitmen, sınıf ve sınıf kayıt iş akışları
- Recurring programdan otomatik `LessonSession` üretimi
- Mobil eğitmen yoklama ekranı ve yoklama değişiklik audit akışı
- Üyelik/fiyat değişikliği uygulama servisi ve yönetim UI'ı
- Fatura, kısmi ödeme, borç ve raporlama ekranları
- Kullanıcı/rol/izin yönetim ekranları
- Gerçek veriye dayalı dashboard göstergeleri

Bu placeholder sayfalar sahte API veya örnek analitik üretmez; yalnızca sonraki fazların navigasyon ve layout sınırlarını gösterir.
