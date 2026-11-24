# REVLINK — Rev up. Link up.

MVP acadêmico de uma rede social automotiva. Android (.NET MAUI 10), ASP.NET Core Minimal API, EF Core e SQLite. Tema All Black + Nitro Lime. Código-fonte inicial; ainda requer compilação e teste em um ambiente .NET/Android.

## 1. Pré-requisitos
- SDK .NET 10 e ambiente MAUI/Android compatível (Visual Studio com workload de desenvolvimento .NET MAUI, ou VS Code com ferramentas MAUI; SDK Android e emulador/dispositivo).
- Para CLI: `dotnet workload install maui-android` (se necessário).
- Confira `dotnet --list-sdks` e `dotnet workload list`.

## 2. Subir a API primeiro
Abra um terminal na raiz da solution:

```powershell
dotnet run --project RevLink.Api --urls http://0.0.0.0:5080
```

Teste em http://localhost:5080/health — deve retornar `{"status":"ok","app":"RevLink"}`. A definição OpenAPI está em http://localhost:5080/openapi/v1.json (JSON, sem interface Swagger UI).

O banco `revlink.db` é criado automaticamente por `EnsureCreatedAsync()`, junto com `revlink.db.key`, a chave local de JWT. Mantenha ambos e não os envie ao Git. A API guarda a senha com hash, e as fotos como BLOB JPEG/PNG no SQLite. Para alterar o esquema durante a fase inicial, faça backup e recrie o banco de teste; não há migrations neste MVP.

## 3. Executar o Android
Abra `RevLink.sln` no Visual Studio e selecione `RevLink.Mobile` como projeto de inicialização. Mantenha a API rodando em um terminal separado. Selecione o emulador Android e execute. O endereço padrão é `http://10.0.2.2:5080`.

Alternativamente, com o emulador aberto e CLI configurada:

```powershell
dotnet build RevLink.Mobile -t:Run -f net10.0-android
```

**Celular físico:** conecte o PC e o celular à mesma rede Wi-Fi, execute `ipconfig` no PC e identifique seu IPv4 (ex.: 192.168.1.50). Na tela de login toque em “Configurar endereço da API” e informe `http://192.168.1.50:5080` substituindo pelo seu IP. Autorize a porta TCP 5080 apenas na rede privada do Windows, se necessário. Teste o `/health` pelo navegador do celular. Não exponha essa porta para a Internet.

HTTP sem TLS é habilitado no manifesto somente para facilitar a demonstração em rede local confiável, com credenciais fictícias. Para implantação pública, use HTTPS, desabilite cleartext no manifesto e configure a chave JWT por variável de ambiente (`Jwt__Key`). Tokens duram 7 dias; o logout remove o token do dispositivo, sem revogação no servidor. Alterar senha não invalida tokens já emitidos neste MVP.

## 4. O que existe no código
- Cadastro/login, confirmação de senha, sessão persistida por SecureStorage, logout e edição de DriverTag/Drivatar/senha.
- Feed cronológico com uma foto por post, legenda, vínculo opcional ao próprio veículo, curtidas e exclusão pelo autor.
- Garagem pessoal, cadastro/edição/exclusão de veículo, detalhes e exploração global com pesquisa simples.
- Eventos: criação/edição/exclusão, inscrições/cancelamento, lista de participantes, remoção pelo anfitrião, capacidade opcional (inclui o anfitrião), bloqueio de duplicidades e eventos lotados/encerrados.
- Tema compartilhado em `Resources/Styles`, navegação e login em XAML; páginas e formulários dinâmicos construídos em C# para reduzir tempo de implementação. Não segue MVVM integral ainda.
- Seis tabelas: Users, Cars, Posts, PostLikes, Events, EventMembers. Chaves compostas para curtidas/inscrições. Ao excluir veículo, os posts permanecem sem vínculo; ao excluir post/evento, curtidas/inscrições são excluídas.

Fora do escopo: comentários, FIPE, mapas, chats, seguidores, recuperação de senha, múltiplas fotos, capa de evento, paginação, moderação e hospedagem pública definitiva. As fotos são redimensionadas para JPEG no Android; rotação EXIF ainda não é tratada. O feed usa ScrollView para o volume pequeno de uma demonstração. URLs de imagens são públicas por ID, sem dados privados do perfil.

## 5. Teste de aceitação (dois usuários)
1. Crie duas contas fictícias A e B.
2. A cadastra um veículo (ex.: SilverBlade, Honda Civic, 2013, 1.8 i-VTEC, Street) com foto.
3. A publica uma foto associada ao veículo; B encontra o carro na aba Explorar e curte o post.
4. A cria um evento com limite 2. B entra e a quantidade passa de 1/2 para 2/2. A terceira conta deve receber “Evento lotado”.
5. B tenta editar um evento de A pela API: deve receber 403. A remove B; B atualiza a lista e pode inscrever-se novamente.
6. A altera perfil, sai, entra novamente. Reinicie a API e confirme que os dados continuam no SQLite.
7. Teste excluir carro, post e evento. Verifique as relações e permissões.

Há também um smoke test Python, sem dependências externas, para executar contra a API em funcionamento:

```powershell
python tests/smoke.py
```

Ele cria contas e dados de teste únicos; não use em uma API de produção. Este script foi incluído para execução no seu ambiente, não foi executado contra a API neste pacote.

## 6. Gerar APK (depois do teste Debug)

```powershell
dotnet publish RevLink.Mobile -f net10.0-android -c Release -p:AndroidPackageFormats=apk
```

Procure o APK assinado de desenvolvimento na pasta de saída de publish (ou use a opção de arquivamento/assinatura do Visual Studio). Para distribuição real, configure a sua keystore. O APK não está incluído no ZIP.

## 7. Hospedagem
Para a entrega mais rápida, use o PC como servidor. O APK roda no Android; apenas a API fica hospedada no servidor. Render gratuito não possui disco persistente: não use o SQLite local lá se precisar preservar contas, fotos e eventos. O Dockerfile incluído suporta implantação em um host com volume persistente `/data`. Exemplo local:

```powershell
docker build -t revlink-api RevLink.Api
docker run --rm -p 5080:8080 -v revlink-data:/data revlink-api
```

Para uma hospedagem gratuita externa que mantenha SQLite, considere um servidor com armazenamento persistente ou a adaptação da camada de dados a um banco compatível (como Turso); esta última não é automática e não está implementada. Priorize concluir o teste local antes disso.
