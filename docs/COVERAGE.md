# Cobertura de stdlib por función — matriz

Generada por `goclr coverage`. Cada paquete: % de su API exportada (funcs +
métodos sobre tipos exportados + vars) que goclr cubre (shim o compilado de fuente).
`src` = compilado de fuente real (cobertura total). Guía hacia el soporte completo
del estándar — GORM y libs grandes caen solas a medida que sube la cobertura.

```
goclr stdlib coverage — per-function matrix
package                             cover    ok/tot   
------------------------------------------------------------------------------
bufio                                 65%    33/51     
bytes                                 54%    52/96     
cmp                                  100%     3/3    src
compress/flate                        42%     5/12     
compress/gzip                         77%    10/13     
compress/zlib                         67%     8/12     
container/heap                       100%     5/5     ✓ 
container/list                        78%    14/18     
container/ring                       100%     8/8    src
context                               71%    10/14     
crypto                                57%     4/7      
crypto/aes                            50%     1/2      
crypto/cipher                          8%     1/13     
crypto/ecdsa                          27%     4/15     
crypto/ed25519                         9%     1/11     
crypto/elliptic                       27%     4/15     
crypto/hmac                          100%     2/2     ✓ 
crypto/md5                           100%     2/2     ✓ 
crypto/rand                           60%     3/5      
crypto/rsa                            25%     6/24     
crypto/sha1                          100%     2/2     ✓ 
crypto/sha256                        100%     4/4     ✓ 
crypto/sha3                           77%    23/30     
crypto/sha512                         50%     4/8      
crypto/subtle                         62%     5/8      
crypto/tls                            35%    22/63     
crypto/x509                           24%    16/67     
database/sql                         100%    89/89   src
database/sql/driver                  100%    14/14   src
encoding/asn1                         17%     2/12     
encoding/base32                       20%     3/15     
encoding/base64                       61%    11/18     
encoding/binary                       65%    11/17     
encoding/csv                          31%     5/16     
encoding/hex                          50%     7/14     
encoding/json                         59%    20/34     
encoding/pem                         100%     3/3     ✓ 
encoding/xml                          44%    15/34     
errors                                57%     4/7      
flag                                   1%     1/72     
fmt                                   57%    13/23     
go/ast                                 1%     1/151    
hash                                 100%     0/0     ✓ 
hash/adler32                         100%     2/2     ✓ 
hash/crc32                            86%     6/7      
hash/fnv                              67%     4/6      
hash/maphash                          44%     7/16     
html                                 100%     2/2     ✓ 
html/template                         50%    15/30     
io                                   100%    37/37   src
io/fs                                 48%    13/27     
iter                                 100%     2/2    src
log                                   82%    28/34     
log/slog                              21%    22/103    
maps                                 100%    10/10   src
math                                  67%    45/67     
math/big                              34%    49/146    
math/bits                             63%    31/49     
math/rand                             58%    21/36     
math/rand/v2                          25%    13/53     
mime                                  22%     2/9      
mime/multipart                        52%    11/21     
net                                   30%    59/196    
net/http                              51%    84/164    
net/http/cookiejar                   100%     3/3     ✓ 
net/http/httptest                     61%    11/18     
net/http/httptrace                   100%     2/2    src
net/http/httputil                      4%     1/27     
net/mail                               9%     1/11     
net/textproto                          9%     3/33     
net/url                               42%    17/40     
os                                    45%    65/144    
os/exec                               32%     6/19     
os/signal                             67%     4/6      
path                                  78%     7/9      
path/filepath                         46%    11/24     
reflect                               57%    68/119    
regexp                                44%    21/48     
runtime                               31%    16/51     
runtime/debug                         33%     5/15     
slices                               100%    40/40   src
sort                                 100%    33/33   src
strconv                               53%    20/38     
strings                               75%    59/79     
sync                                  76%    26/34     
sync/atomic                           65%    56/86     
syscall                                6%    12/215    
testing                              100%    42/42   src
testing/internal/testdeps            100%    25/25   src
text/tabwriter                       100%     4/4    src
text/template                         45%    14/31     
time                                  77%    66/86     
unicode                              100%   282/282  src
unicode/utf16                         71%     5/7      
unicode/utf8                          93%    14/15     
------------------------------------------------------------------------------
TOTAL                                 51%  1853/3632  (95 packages)
```

## Mayores brechas (más trabajo para cerrar)

```
syscall                                6%    12/215    
go/ast                                 1%     1/151    
net                                   30%    59/196    
math/big                              34%    49/146    
log/slog                              21%    22/103    
net/http                              51%    84/164    
os                                    45%    65/144    
flag                                   1%     1/72     
crypto/x509                           24%    16/67     
reflect                               57%    68/119    
bytes                                 54%    52/96     
crypto/tls                            35%    22/63     
math/rand/v2                          25%    13/53     
runtime                               31%    16/51     
net/textproto                          9%     3/33     
sync/atomic                           65%    56/86     
regexp                                44%    21/48     
net/http/httputil                      4%     1/27     
net/url                               42%    17/40     
math                                  67%    45/67     
strings                               75%    59/79     
```
