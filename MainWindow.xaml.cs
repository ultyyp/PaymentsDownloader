using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PaymentsDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        public MainWindow()
        {
            InitializeComponent();
            HideElements();
        }

        public static CancellationTokenSource cts = new CancellationTokenSource();
        public static CancellationToken ct = cts.Token;
        public static bool contiue = false;

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            ShowElements();
            string url = "https://github.com/rodion-m/SystemProgrammingCourse2022/raw/master/files/payments_19mb.zip";
            string outputPath = @"C:\Payments2\output.txt";

            
            await DownloadFileAsync(url, outputPath, ct);

 

        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            cts.Cancel();
        }

        public class SeekStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly MemoryStream _innerStream;
            private readonly int _bufferSize = 64 * 1024;
            private readonly byte[] _buffer;

            public SeekStream(Stream baseStream)
                : this(baseStream, 64 * 1024)
            {
            }

            public SeekStream(Stream baseStream, int bufferSize) : base()
            {
                _baseStream = baseStream;
                _bufferSize = bufferSize;
                _buffer = new byte[_bufferSize];
                _innerStream = new MemoryStream();
            }

            public override bool CanRead
            {
                get { return _baseStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _baseStream.CanRead; }
            }

            public override bool CanWrite
            {
                get { return false; }
            }

            public override long Position
            {
                get { return _innerStream.Position; }
                set
                {
                    if (value > _baseStream.Position)
                        FastForward(value);
                    _innerStream.Position = value;
                }
            }

            public Stream BaseStream
            {
                get { return _baseStream; }
            }

            public override void Flush()
            {
                _baseStream.Flush();
            }

            private void FastForward(long position = -1)
            {
                while ((position == -1 || position > this.Length) && ReadChunk() > 0)
                {
                    // fast-forward
                }
            }

            private int ReadChunk()
            {
                int thisRead, read = 0;
                long pos = _innerStream.Position;
                do
                {
                    thisRead = _baseStream.Read(_buffer, 0, _bufferSize - read);
                    _innerStream.Write(_buffer, 0, thisRead);
                    read += thisRead;
                } while (read < _bufferSize && thisRead > 0);
                _innerStream.Position = pos;
                return read;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                FastForward(offset + count);
                return _innerStream.Read(buffer, offset, count);
            }

            public override int ReadByte()
            {
                FastForward(this.Position + 1);
                return base.ReadByte();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long pos = -1;
                if (origin == SeekOrigin.Begin)
                    pos = offset;
                else if (origin == SeekOrigin.Current)
                    pos = _innerStream.Position + offset;
                FastForward(pos);
                return _innerStream.Seek(offset, origin);
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (!disposing)
                {
                    _innerStream.Dispose();
                    _baseStream.Dispose();
                }
            }

            public override long Length
            {
                get { return _innerStream.Length; }
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }


        public async Task DownloadFileAsync(string url, string outputPath, CancellationToken ct)
        {
            if(ct.IsCancellationRequested && contiue == false)
            {
                contiue = true;
            }

            long fileLength = 0;
            using var httpClient = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, url)
            {
                Headers = { Range = new RangeHeaderValue(fileLength, null) }
            };
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? contentLength = response.Content.Headers.ContentLength;
            if(contentLength == null)
            {
                throw new Exception("Content Length Was Null!");
            }



            if (contiue == true)
            {
                FileInfo fi = new FileInfo(outputPath);
                if (fi.Exists)
                {
                    fileLength = fi.Length;
                }
                
                
                await using SeekStream contentStream = await response.Content.ReadAsStreamAsync() as SeekStream;

               
                

                
                

                byte[] buffer = new byte[8192 * 2];
                //var result = new List<byte>((int)contentLength.GetValueOrDefault());
                progressBar.Maximum = contentLength.Value;


                contentStream.Position = fileLength;

                while (true)
                {
                    //MessageBox.Show("File length:" + fileLength.ToString() + " Content length:" + contentLength.ToString());


                    int bytesRead = await contentStream.ReadAsync(buffer);
                    

                    if (ct.IsCancellationRequested && contiue == false)
                    {
                        break;
                    }


                    if (bytesRead == 0)
                    {
                        break;
                    }

                    if (bytesRead == buffer.Length)
                    {
                        //result.AddRange(buffer);
                        await AppendAllBytesAsync(outputPath, buffer);
                        progressBar.Value += buffer.Length;
                        fileLength += buffer.Length;
                        progressLabel.Content = $"Downloading: {fileLength} / {contentLength}";
                    }
                    else
                    {
                        //result.AddRange(buffer[..bytesRead]);
                        await AppendAllBytesAsync(outputPath, buffer[..bytesRead]);
                        progressBar.Value += buffer[..bytesRead].Length;
                        fileLength += buffer[..bytesRead].Length;
                        progressLabel.Content = $"Downloading: {fileLength} / {contentLength}";
                    }

                }
            }

            else if (contiue == false)
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);

                byte[] buffer = new byte[8192 * 2];
                //var result = new List<byte>((int)contentLength.GetValueOrDefault());
                progressBar.Maximum = contentLength.Value;

                while (true)
                {


                    int bytesRead = await contentStream.ReadAsync(buffer);



                    if (ct.IsCancellationRequested && contiue == false)
                    {
                        break;
                    }


                    if (bytesRead == 0)
                    {
                        break;
                    }

                    if (bytesRead == buffer.Length)
                    {
                        //result.AddRange(buffer);
                        await AppendAllBytesAsync(outputPath, buffer);
                        progressBar.Value += buffer.Length;
                        fileLength += buffer.Length;
                        progressLabel.Content = $"Downloading: {fileLength} / {contentLength}";
                    }
                    else
                    {
                        //result.AddRange(buffer[..bytesRead]);
                        await AppendAllBytesAsync(outputPath, buffer[..bytesRead]);
                        progressBar.Value += buffer[..bytesRead].Length;
                        fileLength += buffer[..bytesRead].Length;
                        progressLabel.Content = $"Downloading: {fileLength} / {contentLength}";
                    }

                }





            }

            
        }

        public static async Task AppendAllBytesAsync(string path, byte[] bytes)
        {
            //argument-checking here.
            await Task.Run(async() => { 
                using (var stream = new FileStream(path, FileMode.Append))
                {
                    if (File.Exists(path))
                    {
                        await stream.WriteAsync(bytes, 0, bytes.Length);
                    }
                }

            });

        }









        void ShowElements()
        {
            cancelButton.IsEnabled = true;
            cancelButton.Focusable = true;
            cancelButton.Visibility = Visibility.Visible;
            progressBar.IsEnabled = true;
            progressBar.Focusable = true;
            progressBar.Visibility = Visibility.Visible;
            progressLabel.IsEnabled = true;
            progressLabel.Focusable = true;
            progressLabel.Visibility = Visibility.Visible;
        }

        void HideElements()
        {
            cancelButton.IsEnabled = false;
            cancelButton.Focusable = false;
            cancelButton.Visibility = Visibility.Collapsed;
            progressBar.IsEnabled = false;
            progressBar.Focusable = false;
            progressBar.Visibility = Visibility.Collapsed;
            progressLabel.IsEnabled = false;
            progressLabel.Focusable = false;
            progressLabel.Visibility = Visibility.Collapsed;
        }

        
    }
}
