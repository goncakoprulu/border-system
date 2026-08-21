# Hosting desteği için PostgreSQL TLS teşhis notu

`panel.border.com.tr` üzerindeki Windows/IIS Plesk uygulaması harici Neon PostgreSQL endpoint'ine TCP seviyesinde ulaşabiliyor; ancak TLS handshake tutarlı biçimde aşağıdaki hatayla sonlanıyor:

```text
System.Security.Authentication.AuthenticationException: TLS alert: HandshakeFailure
Win32Exception 0x80090326: The message received was unexpected or badly formatted
```

Direct ve pooled PostgreSQL endpoint'leri ayrı ayrı denenmiştir. Npgsql tarafında `SSL Mode=VerifyFull` ile sertifika ve hostname doğrulaması korunmuştur. TLS 1.2 açıkça zorlandığında da hata devam ederse lütfen sunucu tarafında aşağıdakileri kontrol edin ve sonuçları paylaşın:

1. Windows Server sürümü, edition ve tam OS build numarası.
2. İlgili Windows Server sürümü için güncel güvenlik ve Schannel güncellemelerinin kurulu olup olmadığı.
3. Schannel istemci tarafında TLS 1.2'nin etkin olup olmadığı; registry veya güvenlik ilkeleriyle devre dışı bırakılıp bırakılmadığı.
4. Etkin TLS 1.2 cipher suite listesinin güncel PostgreSQL/Neon TLS endpoint'leriyle ortak bir cipher içerip içermediği.
5. Windows Event Viewer içindeki Schannel event kayıtları ve handshake failure için event ID/alert ayrıntıları.
6. Sunucudan outbound TCP 5432 trafiğine SSL inspection, DPI, antivirüs, firewall veya hosting ağı tarafından müdahale edilip edilmediği.
7. Hem Neon direct hem pooled hostname'lerine outbound PostgreSQL TLS bağlantısının sunucu veya ağ seviyesinde engellenmeden geçirildiği.
8. Aynı Windows hesabı/application pool bağlamından TLS 1.2 kullanan bağımsız bir PostgreSQL istemcisiyle bağlantının kurulup kurulamadığı.

Lütfen connection string, kullanıcı adı veya parola istemeden; OS build, etkin TLS 1.2 cipher listesi, ilgili Schannel event ayrıntıları ve outbound filtreleme sonucunu paylaşın.
