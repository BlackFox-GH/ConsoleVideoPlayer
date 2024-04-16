using Accord.Video.FFMPEG;
using ConsoleVideoPlayer.ConsoleRenderModes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Media;

namespace ConsoleVideoPlayer
{
    internal class DisplayImpl_Console : IDisplay//az IDisplay parancssoros videólejátszáshoz készített implementációja
    {
        //mivel a képkockákat külön bufferekben tárolom, és erre a C# Console osztálya nem képes, ezért
        //DllImport segítségével a kernel32.dll-en keresztül érem el a parancssornak ezt a funkcióját

        [DllImport("kernel32.dll", SetLastError = true)]
        //Új buffer létrehozása
        private static extern IntPtr CreateConsoleScreenBuffer(
        long dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwFlags,
        IntPtr lpScreenBufferData
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        //Buffer objektum lekérése, "-11" az alapértelmezett buffer amit a Console kezel
        private static extern IntPtr GetStdHandle(
        int nStdHandle
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        //Aktív buffer cseréje
        private static extern bool SetConsoleActiveScreenBuffer(
        IntPtr hConsoleOutput
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        //Írás az adott bufferre, mert a Console.Write() csak a "-11"-esre tud írni
        private static extern bool WriteConsole(
        IntPtr hConsoleOutput,
        char[] lpBuffer,
        long nNumberOfCharsToWrite,
        long lpNumberOfCharsWritten,
        IntPtr lpReserved
        );

        private int bufferSize, processedFrames = 0, renderedFrame = 0, currentFrame = 0, endFrame, desiredFrame = 0;
        private IConsoleFrameRenderer frameRenderer;
        private Dictionary<int, IntPtr> frameBuffer;//képkocka index és képkocka pointer párokat tartalmaz
        private MediaPlayer musicPlayer = new MediaPlayer();//A hangot lejátszó osztály
        private VideoFileReader vfr = new VideoFileReader();//A képkockákat bitmap-ba kiolvasó osztály, az Accord.Video.FFMPEG namespace-ből
        private Stopwatch syncTimer = new Stopwatch();//A kép és hang szinkronjáért felelős időmérő
        private Thread frameBufferFiller, playerThread;
        private ManualResetEvent playing = new ManualResetEvent(true);//ezzel történik a lejátszás megállítása
        private int width;
        private int height;
        private int volume = 50;
        private int endSecs, endMins;

        private IntPtr activeBuffer = IntPtr.Zero;//pointer az aktuális bufferre

        public bool isEnded = false;
        public bool isPlaying = false;

        bool IDisplay.isEnded => isEnded;
        bool IDisplay.isPlaying => isPlaying;

        public DisplayImpl_Console(string path, int bufferSize, IConsoleFrameRenderer renderer, int height)
        {
            this.bufferSize = bufferSize;
            frameRenderer = renderer;
            this.height = frameRenderer.GetPreferredPixelCount() == PreferredPixelCount.ONE_PER_CHAR ? height : height * 2;//képkocka magasság beállítása
            frameBuffer = new Dictionary<int, IntPtr>();
            musicPlayer.Open(new Uri(path, UriKind.RelativeOrAbsolute));//megnyitás zene lejátszásához
            vfr.Open(path);//megnyitás a képkockák kiszedéséhez
            Console.Title = "Console Video Player - " +new Uri(path,UriKind.Absolute);
            width = vfr.Width / (vfr.Height / height) * 2;//képkocka szélességének meghatározása, a képarány megtartásával
            endFrame = Convert.ToInt32(vfr.FrameCount / vfr.FrameRate.Value * 24);//utolsó képkocka kiszámítása
            int endMs = (int)(endFrame * (1000.0 / 24.0));
            endSecs = (int)Math.Floor(endMs / 1000.0) % 60;
            endMins = (int)Math.Floor((int)Math.Floor(endMs / 1000.0) / 60.0);
            Console.CursorVisible = false;
            Console.SetWindowSize(width, height + 1);//ablak méret állítása
            Console.SetCursorPosition(0, 0);
            frameBufferFiller = new Thread(() => FrameBufferFiller(this));//frame buffer szál létrehozása
            frameBufferFiller.Start();
            playerThread = new Thread(() => Player(this));//lejátszó szál létrehozása
            playerThread.Start();
            startPlay();//lejátszás elindítása
        }

        public IntPtr getFrame(int id)//képkocka lekérése, ha nem található a bufferben, null pointer visszaadása
        {
            IntPtr value;
            if (frameBuffer.TryGetValue(id, out value))
            {
                removeOld();//az előző képkockák törlése a bufferből
                return value;
            }
            else return IntPtr.Zero;
        }

        public void startPlay()//lejátszás indítása
        {
            isPlaying = true;
            playing.Set();
            musicPlayer.Play();
            syncTimer.Start();
        }

        public void stopPlay()//lejátszás megállítása
        {
            isPlaying = false;
            playing.Reset();
            syncTimer.Stop();
            musicPlayer.Pause();
        }

        public void increaseVolume(int amount)//hangerő növelése
        {
            volume += amount;
            if (volume > 100) volume = 100;
            musicPlayer.Volume = (double)volume / 100.0;
            if (!isPlaying) fixVolumeSlider();
        }

        public void decreaseVolume(int amount)//hangerő csökkentése
        {
            volume -= amount;
            if (volume < 0) volume = 0;
            musicPlayer.Volume = (double)volume / 100.0;
            if (!isPlaying) fixVolumeSlider();
        }

        private void fixVolumeSlider()
        /*
        Mivel a lejátszáskor íródik a képkockára az aktuális hangerő, így ha a videó meg van állítva,
        nem látszana a hangerő állítás. Ez a függvény akkor fut, ha a hangerőt állítjuk amikor a videó
        meg van állítva, felülírja az aktuálisan megjelenített bufferben a hangerő részt az új adatokkal
         */
        {
            StringBuilder playerInfo = new StringBuilder();
            playerInfo.Append("\x1b["+(height+1)+";"+(Console.BufferWidth-9)+"H");
            playerInfo.Append(new string((char)355, volume / 10));
            playerInfo.Append(new string('-', 10 - volume / 10));
            playerInfo.Append("\x1b[0;0H");
            WriteToBuffer(activeBuffer, playerInfo.ToString().ToCharArray());
        }

        private void removeOld()//A frame buffer-ből már lejátszott képkockák törlése
        {
            try
            {
                List<int> keys = frameBuffer.Keys.ToList();
                foreach (int key in keys)
                {
                    if (key < currentFrame)
                    {
                        frameBuffer.Remove(key);
                    }
                }
            }
            catch (ArgumentException) 
            {
                /* 
                 * néha ArgumentException-al nem tudja a ToList eltárolni a keys-ben a key-eket,
                 * de mivel ez minden egyes frame esetén meghívódik, így nem nagy probléma ha 
                 * néhány frame-n nem ürítjük a régi képkockákat
                 * 
                 * Ahhoz hogy ez problémát okozzon, egymás után legalább 120 (buffer hossza)
                 * alkalommal meg kellene hogy történjen, egymás után,
                 * általában több tízezerből 1-szer történik meg.
                 */
            }
        }

        private static void WriteToBuffer(IntPtr buffer, char[] text)
        /*
         írás az adott bufferre, a Console.Write()-hez hasonlóan működik, de működik akármelyik bufferrel, nem csak a "-11"-el
         */
        {
            WriteConsole(buffer, text, text.Length, 0, IntPtr.Zero);
        }

        private static void Player(DisplayImpl_Console playerData)//a képkockák megjelenítésére szolgáló függvény
        {
            int freeLength = playerData.width - 25;//kiszámolja mennyi helyünk van az alsó csík megjelenítésére
            do
            {
                playerData.playing.WaitOne();//itt áll meg ha megállítjuk a lejátszást
                playerData.desiredFrame = Convert.ToInt32(Convert.ToDouble(playerData.syncTimer.ElapsedMilliseconds) / (1000.0 / 24.0));//megmondja hányadik képkockát kellene megjelenítenünk
                if (playerData.currentFrame < playerData.desiredFrame)
                {
                    IntPtr frame = playerData.getFrame(playerData.desiredFrame);//lekérjük a bufferből az adott képkockát
                    if (frame != IntPtr.Zero)//ha kaptunk vissza képkockát, akkor megjelenítjük
                    {
                        playerData.activeBuffer = frame;
                        playerData.currentFrame = playerData.desiredFrame;

                        //az alsó haladás csík, és hangerő összeállítása, majd bufferre írása
                        StringBuilder playerInfo = new StringBuilder();
                        playerInfo.Append(string.Format("{0:D2}:{1:D2}", playerData.syncTimer.Elapsed.Minutes, playerData.syncTimer.Elapsed.Seconds));
                        int currentProgress = (int)(freeLength * (double)playerData.currentFrame / (double)playerData.endFrame);
                        playerInfo.Append(new string((char)355, currentProgress));
                        playerInfo.Append(new string('-', freeLength - currentProgress));
                        playerInfo.Append(string.Format("{0:D2}:{1:D2}|vol:", playerData.endMins, playerData.endSecs));
                        playerInfo.Append(new string((char)355, playerData.volume / 10));
                        playerInfo.Append(new string('-', 10 - playerData.volume / 10));
                        WriteToBuffer(frame, playerInfo.ToString().ToCharArray());

                        //buffer beállítása az aktuális képkockára
                        SetConsoleActiveScreenBuffer(frame);
                    }
                }
            } while (playerData.currentFrame < playerData.endFrame-2);
            SetConsoleActiveScreenBuffer(GetStdHandle(-11));//visszaváltás a "-11"-es bufferre, innentől ismét működik a Console.Write()
            Console.Clear();
            Console.WriteLine("A lejátszás befejeződött! Nyomj meg egy gombot a kilépéshez...");
            playerData.isEnded = true;
            playerData.isPlaying = false;
        }

        private static void FrameBufferFiller(DisplayImpl_Console playerData)//a képkockák létrehozására szolgáló függvény
        {
            //A videó lehet akármennyi FPS (Frames Per Second), azért hogy biztos jól meg tudjuk jeleníteni, 24FPS-re konvertáljuk
            //
            //Ehhez kiszámolunk egy képkockánkénti késleltetést (ami lehet negatív is >24FPS esetén), és ha a késleltetés elér 1 képkockányi
            //csúszást, akkor vagy kihagyunk 1 képkockát, vagy hozzáadjuk az előző képkockát 1-szer
            double delayPerFrame = Math.Round(1 - (playerData.vfr.FrameCount / playerData.vfr.FrameRate.Value * 24 / Convert.ToDouble(playerData.vfr.FrameCount)), 8), skipAmount = 0.0;
            while (playerData.processedFrames < playerData.vfr.FrameCount-1)
            {
                while (playerData.frameBuffer.Count > playerData.bufferSize) {}//amíg a buffer teli, itt megállunk
                skipAmount += delayPerFrame;

                //Új screen buffer létrehozása
                IntPtr frame = CreateConsoleScreenBuffer(0x80000000L | 0x40000000L, 0x00000001 | 0x00000002, IntPtr.Zero, 1, IntPtr.Zero);

                //Új képkocka lekérése, szöveggé alakítása, majd screen bufferre kiírása
                WriteToBuffer(frame, playerData.frameRenderer.getFrameText(new Bitmap(playerData.vfr.ReadVideoFrame(), new Size(playerData.width, playerData.height))).ToCharArray());

                playerData.frameBuffer[playerData.renderedFrame] = frame;//bufferhez adjuk a screen buffert
                playerData.renderedFrame++;
                playerData.processedFrames++;
                while (skipAmount >= 1.0)//ha a késés több mint 1 képkocka, kihagyjuk a következőt(FPS>24)
                {
                    playerData.vfr.ReadVideoFrame();//képkocka beolvasása
                    playerData.processedFrames++;
                    skipAmount -= 1.0;
                    skipAmount += delayPerFrame;
                }
                while (skipAmount < -1.0)//ha a késés kevesebb mint 2 képkocka, az előző képkockát duplikáljuk (FPS<24)
                {
                    playerData.frameBuffer[playerData.renderedFrame] = playerData.frameBuffer[playerData.renderedFrame - 1];
                    playerData.renderedFrame++;
                    skipAmount += 1.0;
                    skipAmount += delayPerFrame;
                }
            }
        }
    }
}
