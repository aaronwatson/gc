using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.IO;
using CommandLine;
using TagLib;
using OpenQA.Selenium.Support.UI;
using System.Net.Http;
using System.Threading.Tasks;

namespace selenium
{
    class Program
    {
        private static string _baseUrl = "https://www.churchofjesuschrist.org/study/general-conference/";
        // selectors
//        private static By _title = By.XPath("/html/head/meta[5]");
        private static By _tocButton = By.XPath("//*[@id='app']/div/main/div/div[2]/header/div[1]/button");
        private static By _title = By.ClassName("itemTitle-MXhtV");
//        private static By _talkUrl = By.ClassName("listTile-WHLxI");
        private static By _talkUrl = By.ClassName("item-U_5Ca");
        private static By _speakerName = By.ClassName("subtitle-LKtQp");
        private static By _audioPlayer = By.CssSelector("[aria-label='Audio Player']");
        private static By _audioMore = By.CssSelector("[aria-label='More']");
        private static By _talkTitle = By.Id("title1");
        private static By _talkMp3Url = By.LinkText("Download Audio");
        private static By _talkText = By.ClassName("body-block");
        private static By _speakerImageUrl = By.XPath("//*[@class='bitmovinplayer-poster']");

        public class Options
        {
            [Option('o', "outputFolder", Required = true, HelpText = "Set path to output folder.")]
            public string OutputFolder { get; set; }

            [Option('y', "year", Required = false, HelpText = "The conference year to download as a 4 digit number.")]
            public string Year { get; set; }

            [Option('m', "month", Required = false, HelpText = "The conference month to download as a 2 digit number.")]
            public string Month { get; set; }
        }

        private static void ClickElement(ChromeDriver driver, By locator)
        {
            WebDriverWait wait = new WebDriverWait(new SystemClock(), driver, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));

            var element = driver.FindElement(locator);

            Func<IWebDriver, bool> isElementDoneAnimating = 
                d => {
                    var transform = d.FindElement(locator).GetCssValue("transform");
                    return (string.Compare(transform, "matrix(1, 0, 0, 1, 0, 0)") == 0 || string.Compare(transform, "none") == 0);
                };
            //wait until the button stops animating
            wait.Until(isElementDoneAnimating);
            element.Click();
        }

        private static async Task<ChromeDriver> StartWebDriver()
        {
            // var installer = new ChromeDriverInstaller();
            // var driverPath = await installer.Install(true);

            // launch headless chrome browser
            var option = new ChromeOptions();
            option.AddArgument("--no-sandbox");
            option.AddArgument("--headless");
            option.AddArgument("--disable-dev-shm-usage");

            var chromeDriverService = ChromeDriverService.CreateDefaultService();
            chromeDriverService.SuppressInitialDiagnosticInformation = true;
            chromeDriverService.HideCommandPromptWindow = true;
            return new ChromeDriver(chromeDriverService, option);
        }

        static async Task Main(string[] args)
        {
            var parseResult = CommandLine.Parser.Default.ParseArguments<Options>(args);
            await parseResult
                .MapResult(
                    async (Options o) => {

                        var spinner = new ConsoleSpinner();
                        Console.CursorVisible = false;
                        Console.WriteLine("Loading General Conference info...");

                        // launch chrome
                        var driver = await StartWebDriver();

                        // navigate to the GC site
                        var confUrl = $"{_baseUrl}{o.Year.Trim()}/{o.Month.Trim()}?lang=eng";
                        driver.Navigate().GoToUrl(confUrl);

                        // click the toc button
                        driver.FindElement(_tocButton).Click();
                        Thread.Sleep(1000);

                        // show the title
                        var talks = driver.FindElements(_title);
                        var title = talks[0].Text;
                        Console.WriteLine($"Found {title}.");

                        // get the list of talk url's
                        var talkUrlList = Program.GetTalkUrls(driver);

                        Console.WriteLine($"Found {talkUrlList.Count} talks.");

                        // get the list of mp3 url's
                        var talkList = new List<TalkInfo>();
                        var i = 0;
                        foreach (var link in talkUrlList)
                        {
                            try 
                            {
                                Console.WriteLine($"\rLoading talk {++i} of {talkUrlList.Count} ");
                                talkList.Add(GetTalkDetails(driver, link));
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"\rError reading talk at {talkUrlList[i]}: {e.Message}");
                            }
                        }
                        Console.WriteLine(" Done.");

                        // close the browser
                        driver.Quit();

                        // start mp3 downloads
                        Console.WriteLine($"Saving all talks to folder: {o.OutputFolder}...");
                        Directory.CreateDirectory(o.OutputFolder);
                        var webClient = new HttpClient();
                        i = 0;
                        foreach (var talkInfo in talkList)
                        {
                            try
                            {
                                Console.Write($"\rDownloading talk {++i} of {talkList.Count} ");
                                spinner.Spin();
                                var fileName = Path.Combine(o.OutputFolder, talkInfo.Mp3FileName);
                                await DownloadFileFromUrl( webClient, talkInfo.Mp3Url, fileName);

                                var imgFileName = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
                                await DownloadFileFromUrl( webClient, talkInfo.SpeakerImageUrl, imgFileName);

                                CreateMp3File(talkInfo, i, talkList.Count, title, fileName, imgFileName);
                                System.IO.File.Delete(imgFileName);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"Unable to download talk. Error: {e.Message}");
                            }
                        }
                        Console.WriteLine(" Done.");

                        Console.WriteLine("All talks downloaded and saved to local folder.");
                        Console.CursorVisible = true;
                    },                    
                    errors => Task.FromResult(-1)
                );
        }

        private static async Task<bool> DownloadFileFromUrl(HttpClient client, string url, string fileName)
        {
            using (var str = await client.GetStreamAsync(url))
            {
                using (var fs = new FileStream(fileName, FileMode.OpenOrCreate))
                {
                    await str.CopyToAsync(fs);
                }
            }

            return true;
        }

        private static void CreateMp3File(TalkInfo talkInfo, int trackNum, int trackCount, string title, string fileName, string imgFileName)
        {
            var mp3File = TagLib.File.Create(fileName);
            mp3File.Tag.Title = talkInfo.Title;
            mp3File.Tag.Album = title.Replace("general conference", "General Conference");
            mp3File.Tag.AlbumArtists = new string[] {"General Conference"};
            mp3File.Tag.Genres = new string[] {"LDS General Conference"};
            mp3File.Tag.Grouping = "LDS General Conference";
            mp3File.Tag.Lyrics = talkInfo.TalkText;
            mp3File.Tag.Track = (uint)trackNum;
            mp3File.Tag.TrackCount = (uint)trackCount;
            Picture picture = new Picture();
            picture.Type = PictureType.Other;
            picture.MimeType = "image/jpeg";
            picture.Description = "Cover";
            picture.Data = ByteVector.FromPath(imgFileName);
            mp3File.Tag.Pictures = new IPicture[1] {picture};
            mp3File.Save();
        }

        private static List<string> GetTalkUrls(ChromeDriver driver)
        {
            var talkUrlList = new List<string>();
            var talkTitle = "";
            foreach (var link in driver.FindElements(_talkUrl))
            {
                // make sure it contains a speaker name
                try
                {
                    talkTitle = link.Text;// link.FindElement(_talkTitle).Text;
                    Console.WriteLine($"Found {talkTitle}.");
                    // the 'primaryMeta' class holds the speaker's name, skip the others (entire session mp3s)
                    if (link.FindElement(_speakerName) != null)
                    {
                        talkUrlList.Add(link.GetAttribute("href"));
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Skipping {talkTitle}.");
                }
            }

            return talkUrlList;
        }

        private static TalkInfo GetTalkDetails(ChromeDriver driver, string talkUrl)
        {
            driver.Navigate().GoToUrl(talkUrl);
            ClickElement(driver, _audioPlayer);
            ClickElement(driver, _audioMore);
            var title = driver.FindElement(_talkTitle).Text;
            var mp3Url = driver.FindElement(_talkMp3Url).GetAttribute("href");
            var talkText = driver.FindElement(_talkText).Text;
            // this fails for some reason on the first talk, so we wait and try again
            Thread.Sleep(200);
            var imageUrl = driver.FindElement(_speakerImageUrl).GetCssValue("background-image");

            // extract the actual url from the string
            if (imageUrl.Length > 7)
                imageUrl = imageUrl.Substring(5, imageUrl.Length - 7);
            else
                imageUrl = string.Empty;

            // set the filename
            var f = mp3Url.LastIndexOf('/') + 1;
            var l = mp3Url.LastIndexOf('?');
            var fileName = $"{mp3Url.Substring(f, l-f)}";
           
            return new TalkInfo{ Title = title, Mp3Url = mp3Url, Mp3FileName = fileName, TalkText = talkText, SpeakerImageUrl = imageUrl };
        }
    }

    internal class TalkInfo
    {
        public string Title {get; set;}
        public string Mp3Url {get; set;}
        public string TalkText {get; set;}
        public string SpeakerImageUrl {get; set;}
        public string Mp3FileName {get; set;}
    }

    internal class ConsoleSpinner
    {
        private int counter = 0;
        public void Spin()
        {
            switch (++counter % 4)
            {
                case 0: Console.Write("—"); break;
                case 1: Console.Write("\\"); break;
                case 2: Console.Write("|"); break;
                case 3: Console.Write("/"); break;
            }
        }
    }
}
