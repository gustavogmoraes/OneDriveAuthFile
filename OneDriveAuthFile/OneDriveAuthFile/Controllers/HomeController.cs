using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.Graph;
using OneDriveAuthFile.TokenStorage;
using System.Configuration;
using System.Net.Http.Headers;
using System.IO;
using OneDriveAuthFile.Negocio.Servicos;

namespace OneDriveAuthFile.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (Request.IsAuthenticated)
            {
                string userName = ClaimsPrincipal.Current.FindFirst("name").Value;
                string userId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;
                if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(userId))
                {
                    // Aqui significa que obtemos a conta mas a mesma está vazia, desloga
                    return RedirectToAction("SignOut");
                }

                // Como guardamos o token em caache da sessão, se o servidor reiniciar
                // mas o browser ainda tiver o cookie cacheado estaremos "autenticados"
                // mas com um token invalido, por isso fazemos essa validação e deslogamos caso necessário
                SessionTokenCache tokenCache = new SessionTokenCache(userId, HttpContext);
                if (!tokenCache.HasData())
                {
                    // Cache vazia, desloga
                    return RedirectToAction("SignOut");
                }

                ViewBag.UserName = userName;
            }
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = string.Empty;

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Error(string message, string debug)
        {
            ViewBag.Message = message;
            ViewBag.Debug = debug;
            return View("Error");
        }

        public void SignIn()
        {
            if (!Request.IsAuthenticated)
            {
                // Envia um sinal para que o OWIN envie uma request de autorização para o Azure
                HttpContext.GetOwinContext().Authentication.Challenge(
                    new AuthenticationProperties { RedirectUri = "/" },  OpenIdConnectAuthenticationDefaults.AuthenticationType);
            }
        }

        public void SignOut()
        {
            if (Request.IsAuthenticated)
            {
                string userId = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    // Limpa o tokenCache
                    SessionTokenCache tokenCache = new SessionTokenCache(userId, HttpContext);
                    tokenCache.Clear();
                }
            }

            // Envia uma request com protocolo OpenId para deslogar 
            HttpContext.GetOwinContext().Authentication.SignOut(
                CookieAuthenticationDefaults.AuthenticationType);
            Response.Redirect("/");
        }

        public async Task<string> ObtenhaTokenDeAcesso()
        {
            string token = null;

            // Carregando as configurações de Web.Config, que chama AzureOauth.config
            string appId = ConfigurationManager.AppSettings["ida:AppId"];
            string appPassword = ConfigurationManager.AppSettings["ida:AppPassword"];
            string redirectUri = ConfigurationManager.AppSettings["ida:RedirectUri"];
            string[] scopes = ConfigurationManager.AppSettings["ida:AppScopes"].Replace(' ', ',')
                                                                               .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            // Obtem id do usuário
            var idDoUsuario = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;

            if (!string.IsNullOrEmpty(idDoUsuario))
            {
                // Obtem o token do cache
                SessionTokenCache tokenCache = new SessionTokenCache(idDoUsuario, HttpContext);

                ConfidentialClientApplication cca = new ConfidentialClientApplication(
                    appId, redirectUri, new ClientCredential(appPassword), tokenCache.GetMsalCacheInstance(), null);

                // Obtendo a conta de usuario e depois chamando "AcquireTokenSilentAsync" que vai obter o token cacheado
                // se ele não tiver expirado. Se tiver, vai tratar e obter um "refresh token" visto que o usuário logou
                var conta = cca.GetAccountsAsync().Result.FirstOrDefault();

                var resposta = await cca.AcquireTokenSilentAsync(scopes, conta);
                token = resposta.AccessToken;
            }

            return token;
        }

        public async Task<ActionResult> Execute()
        {
            string token = await ObtenhaTokenDeAcesso();
            if (string.IsNullOrEmpty(token))
            {
                // Se a sessão não tiver nenhum token encerramos
                return new EmptyResult();
            }

            GraphServiceClient client = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    (requestMessage) =>
                    {
                        requestMessage.Headers.Authorization =
                            new AuthenticationHeaderValue("Bearer", token);

                        return Task.FromResult(0);
                    }));

            try
            {
                // Aqui instancio o Serviço de arquivo, com 3 métodos diferentes, Download direto / Download com prompt / Upload
                using (var servicoDeArquivo = new ServicoDeArquivo(client))
                {
                    // Obs: Os caminhos incluem os nomes dos arquivos e suas terminações
                    // Seguem exemplos
                    var caminhoNoDrive = @"Arquivos Pessoais\Documento1.docx"; //string.Empty;
                    var caminhoNoComputador = @"D:\Teste.docx"; //string.Empty;

                    // Baixa arquivo em diretorio
                    servicoDeArquivo.BaixeArquivoEmDiretorioEspecifico(caminhoNoDrive, caminhoNoComputador);

                    // Baixa arquivo no Controller p/ depois dar prompt para o usuario baixar
                    var conteudo = await servicoDeArquivo.ObtenhaArquivoParaBaixarNoNavegador(caminhoNoDrive);

                    // Faz upload do arquivo
                    var caminhoUpload = @"Arquivos Pessoais\DocumentoDeTeste.docx"; //string.Empty
                    await servicoDeArquivo.FacaUploadDeArquivoNoDrive(conteudo, caminhoUpload);
                }

                return new EmptyResult();
            }

            catch (ServiceException ex)
            {
               return RedirectToAction("Error", "Home", new { message = "Erro", debug = ex.Message });
            }
        }
    }
}