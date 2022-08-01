using System;
using System.Collections.Generic;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Net;
using System.IO;
using CommandLine;
using TagLib;

namespace selenium
{
    class Program
    {
        public class Options
        {
            [Option('o', "outputFolder", Required = true, HelpText = "Set path to output folder.")]
            public string OutputFolder { get; set; }

            [Option('y', "year", Required = false, HelpText = "The conference year to download.")]
            public string Year { get; set; }

            [Option('m', "month", Required = false, HelpText = "The conference month to download.")]
            public string Month { get; set; }
        }

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                        var spinner = new ConsoleSpinner();
                        Console.WriteLine("Installing ChromeDriver");
                        var installer = new ChromeDriverInstaller();
                        installer.Install();

//                        Console.CursorVisible = false;
                        Console.WriteLine("Loading General Conference info...");

                        // launch headless chrome browser
                        var option = new ChromeOptions();
                        option.AddArgument("--no-sandbox");
                        option.AddArgument("--disable-dev-shm-usage");
//                        option.BinaryLocation = "/opt/google/chrome/chrome";
                        var chromeDriverService = ChromeDriverService.CreateDefaultService();
                        chromeDriverService.SuppressInitialDiagnosticInformation = true;
                        chromeDriverService.HideCommandPromptWindow = true;
                        var driver = new ChromeDriver(chromeDriverService, option);

                        // navigate to the GC site
                        var confUrl = "https://www.churchofjesuschrist.org/study/general-conference?lang=eng";
                        if (string.IsNullOrEmpty(o.Month) == false && string.IsNullOrEmpty(o.Year) == false)
                            confUrl = $"https://www.churchofjesuschrist.org/study/general-conference/{o.Year}/{o.Month}?lang=eng";
                        driver.Navigate().GoToUrl(confUrl);

                        // show the title
                        var title = driver.FindElement(By.ClassName("title")).Text;
                        Console.WriteLine($"Found {title}.");

                        // get the list of talk url's
                        var talkUrlList = new List<string>();
                        foreach (var link in driver.FindElements(By.ClassName("lumen-tile__link")))
                        {
                            talkUrlList.Add(link.GetAttribute("href"));
                        }

                        Console.WriteLine($"Found {talkUrlList.Count} talks.");

                        // get the list of mp3 url's
                        var mp3List = new List<(string title, string mp3Url, string mp3Text, string mp3ImageUrl)>();
                        var i = 0;
                        foreach (var link in talkUrlList)
                        {
                            Console.Write($"\rLoading talk {++i} of {talkUrlList.Count} ");
                            try {
                                spinner.Spin();
                                driver.Navigate().GoToUrl(link);
//                                driver.FindElement(By.Id("triggerdownload").Click();
                                driver.FindElement(By.XPath("//*[@title='View Downloads']")).Click();
//                                driver.FindElement(By.XPath("//*[@id=\"triggerdownload\"]").Click();
                                Thread.Sleep(100);
                                var mp3Title = driver.FindElement(By.Id("title1")).Text;
                                var mp3Url = driver.FindElement(By.LinkText("This Page (MP3)")).GetAttribute("href");
                                var mp3Text = driver.FindElement(By.ClassName("body-block")).Text;
                                var imageUrl = driver.FindElement(By.XPath("//video[contains(@data-ip,'Download')]")).GetAttribute("poster");
                                mp3List.Add((mp3Title, mp3Url, mp3Text, imageUrl));
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
                        var outDir = $"/out/{title}";
                        Directory.CreateDirectory(o.OutputFolder);
                        var webClient = new WebClient();
                        i = 0;
                        foreach (var mp3Info in mp3List)
                        {
                            Console.Write($"\rDownloading talk {++i} of {mp3List.Count} ");
                            spinner.Spin(); 
                            // get the filename and download to local folder
                            var f = mp3Info.mp3Url.LastIndexOf('/') + 1;
                            var l = mp3Info.mp3Url.LastIndexOf('?');
                            var fileName = $"{o.OutputFolder}/{mp3Info.mp3Url.Substring(f, l-f)}";
                            webClient.DownloadFile(mp3Info.mp3Url, fileName);
                            var imageStream = new MemoryStream(webClient.DownloadData(mp3Info.mp3ImageUrl));
                            using (imageStream)
                            {
                                //update the mp3 tags
                                var mp3File = TagLib.File.Create(fileName);
                                mp3File.Tag.Title = mp3Info.title;
                                mp3File.Tag.Album = title.Replace("general conference", "General Conference");
                                mp3File.Tag.AlbumArtists = new string[] {"General Conference"};
                                mp3File.Tag.Genres = new string[] {"LDS General Conference"};
                                mp3File.Tag.Grouping = "LDS General Conference";
                                mp3File.Tag.Lyrics = mp3Info.mp3Text;
                                mp3File.Tag.Track = (uint)i;
                                mp3File.Tag.TrackCount = (uint)mp3List.Count;
                                Picture picture = new Picture();
                                picture.Type = PictureType.Other;
                                picture.MimeType = "image/jpeg";
                                picture.Description = "Cover";        
                                picture.Data = ByteVector.FromStream(imageStream);
                                mp3File.Tag.Pictures = new IPicture[1] {picture};
                                mp3File.Save();
                            }
                        }
                        Console.WriteLine(" Done.");

                        Console.WriteLine("All talks downloaded and saved to local folder.");
                        Console.CursorVisible = true;
                    });
        }
    }

    public class ConsoleSpinner
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
