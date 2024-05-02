using System;
using System.Windows.Forms;
using System.Threading;
using ConsoleVideoPlayer.ConsoleRenderModes;
using Bfx.Console;

namespace ConsoleVideoPlayer
{

    class Program
    {
        static OpenFileDialog ofd = new OpenFileDialog();
        static bool renderMode = true;
        static void OpenFile()//fájl választó ablak megnyitása, ha nem választunk, kilép
        {
            ofd.Filter = "Videók (*.mp4)|*.mp4";
            ofd.ShowDialog();
            if (ofd.FileName == "")
            {
                Console.Clear();
                Console.WriteLine("Nem választottál fájlt, nyomj ENTER-t a kilépéshez.");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }

        static void Main(string[] args)
        {
            ConsoleHandle.handleColorEscape();//ASCII escape karakterek kezelésének bekapcsolása
            DisplayImpl_Console videoPlayer;
            ConsoleKeyInfo input;
            if (args.Length > 0 && (args[0].EndsWith(".mp4") || args[0].EndsWith(".mkv")))//ha indítási paramétert (args) adtunk meg, akkor az elsőt a videó elérési útjának kezeli, ha jó a formátuma
            {
                videoPlayer = new DisplayImpl_Console(args[0], 120, new ConsoleFrameRendererImpl_Gradient(), 50);
            }
            else
            {
                do//A megjelenítő választó menü
                {
                    Console.Title = "Console Video Player";
                    Console.SetCursorPosition(0, 0);
                    Console.WriteLine("Nyomj ENTER-t a videófájl kiválasztásához vagy \"R\" betűt a megjelenítő váltásához...");
                    input = Console.ReadKey();
                    if (input.KeyChar == 'r')
                    {
                        renderMode = !renderMode;
                        Console.WriteLine("\r" + (renderMode ? "Szürkeárnyalatos" : "Fekete-fehér") + " megjelenítés kiválasztva    ");
                    }
                } while (input.Key != ConsoleKey.Enter);
                Thread th = new Thread(OpenFile);//fájl választó megnyitása külön szálon
                th.SetApartmentState(ApartmentState.STA);//erre azért van szükség, mert a Forms-os fájlválasztó ablak csak így nyílik meg
                th.Start();
                th.Join();
                Console.SetCursorPosition(0, 0);
                IConsoleFrameRenderer renderer;//lértehozzuk a renderer-t, ami feldolgozza majd a képkockákat
                if (renderMode)
                {
                    renderer = new ConsoleFrameRendererImpl_24Bit();
                }
                else
                {
                    renderer = new ConsoleFrameRendererImpl_2Color();
                }
                videoPlayer = new DisplayImpl_Console(ofd.FileName, 120, renderer, 50);//létrehozzuk a lejátszót, ami el is indul magától
            }
            do//Az indítás/megállítás és hangerő kezelésért felel ez a do..while, a lejátszás végén megnyomandó gomb ebből a ciklusból léptet ki
            {
                input = Console.ReadKey(true);
                if (input.Key == ConsoleKey.Spacebar)//indítás/megállítás
                {
                    if (videoPlayer.isPlaying)
                    {
                        videoPlayer.stopPlay();
                    }
                    else
                    {
                        videoPlayer.startPlay();
                    }
                }
                else if (input.Key == ConsoleKey.UpArrow)//hangerő+
                {
                    videoPlayer.increaseVolume(5);
                }
                else if (input.Key == ConsoleKey.DownArrow)//hangerő-
                {
                    videoPlayer.decreaseVolume(5);
                }
            } while (!videoPlayer.isEnded);
        }
    }
}
