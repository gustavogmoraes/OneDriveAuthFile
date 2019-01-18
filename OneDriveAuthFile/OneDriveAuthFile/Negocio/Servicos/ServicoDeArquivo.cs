using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Resources;
using Microsoft.Identity.Client;
using System.Windows.Forms;

namespace OneDriveAuthFile.Negocio.Servicos
{
    public class ServicoDeArquivo : IDisposable
    {
        private GraphServiceClient Client { get; set; }

        public ServicoDeArquivo(GraphServiceClient client)
        {
            Client = client;
        }

        public async void BaixeArquivoEmDiretorioEspecifico(string caminhoNoDrive, string caminhoNoComputador)
        {
            using (var stream = await Client.Drive.Root.ItemWithPath(caminhoNoDrive).Content.Request().GetAsync())
            using (var outputStream = new System.IO.FileStream(caminhoNoComputador, FileMode.Create))
            {
                await stream.CopyToAsync(outputStream);
            }
        }

        public async Task<Stream> ObtenhaArquivoParaBaixarNoNavegador(string caminhoNoDrive)
        {
            var stream = await Client.Drive.Root.ItemWithPath(caminhoNoDrive).Content.Request().GetAsync();
            return stream;
        }

        public async Task FacaUploadDeArquivoNoDrive(Stream conteudo, string caminhoNoDrive)
        {
            var uploaded = await Client.Me.Drive.Root.ItemWithPath(caminhoNoDrive).Content.Request().PutAsync<DriveItem>(conteudo);
        }

        #region IDisposable

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Client = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}