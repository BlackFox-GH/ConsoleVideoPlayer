using ConsoleVideoPlayer.ConsoleRenderModes;
using System.Drawing;
using System.Text;

namespace ConsoleVideoPlayer.ConsoleRenderModes
{
    internal class ConsoleFrameRendererImpl_24Bit : IConsoleFrameRenderer//24bites megjelenítő mód, "▄" karakter és ASCII escape-k használatával
    /*
        A 3 megjelenítő közül ez a legjobb, mivel akármilyen szín megjeleníthető,
        de ennek van egy nagy hátránya. Amíg a képkocka szöveggé alakított változata
        a másik 2 megjelenítővel nem szokott hosszabb lenni 10 ezer karakternél, ennél
        általában egy képkocka olyan 100-200 ezer karakter hosszú, legrosszabb esetben
        300 ezer karakternél is hosszabb lehet. A jelenlegi módszerrel nem tudom a képkockát
        elég gyorsan feldolgozni, kiírni egy bufferre, megjeleníteni úgy, hogy tartani tudja a
        24 FPS-t. Az ASCII escape-ek túl hosszúvá teszik, ezért próbáltam meg az előző színt
        tárolni, így csökkentve a szükséges ASCII escape-k számát.

        Jelenlegi helyzetben, ha ezt a megjelenítőt használnánk, a kép megakadna, mivel a buffer
        kifogy a képekből.
    */
    {
        public ConsoleFrameRendererImpl_24Bit()
        {
            this.preferredPixelCount = PreferredPixelCount.TWO_PER_CHAR;//itt két pixelt kell néznünk
        }

        public override string getFrameText(Bitmap frame)
        {

            int[] p1 = { -1, -1, -1 };//előző felső pixel színe
            int[] p2 = { -1, -1, -1 };//előző alsó pixel színe
            int quality = 64;
            StringBuilder sb = new StringBuilder();
            lock (frame)
            {
                for (int y = 1; y < frame.Height; y += 2)
                {
                    for (int x = 0; x < frame.Width; x++)
                    {
                        Color px = frame.GetPixel(x, y - 1), px2 = frame.GetPixel(x, y);//lekérjük a pixelek színét
                        if (p1[0] != px.R/quality || p1[1] != px.G / quality || p1[2] != px.B / quality)//ha az alsó nem egyezik, állítjuk a karakter színét
                        {
                            p1[0] = px.R / quality;
                            p1[1] = px.G / quality;
                            p1[2] = px.B / quality;
                            sb.Append($"\x1b[38;2;");//háttér szín állítás ASCII escape-el
                            sb.Append(p1[0]* quality);
                            sb.Append(";");
                            sb.Append(p1[1] * quality);
                            sb.Append(";");
                            sb.Append(p1[2] * quality);
                            sb.Append("m");
                            //sb.Append($"\x1b[38;2;{p1[0]};{p1[1]};{p1[2]}m");//karakter szín állítás ASCII escape-el
                        }
                        if (p2[0] != px2.R/ quality || p2[1] != px2.G / quality || p2[2] != px2.B/ quality)//ha a felső nem egyezik, állítjuk a háttér színét
                        {
                            p2[0] = px2.R / quality;
                            p2[1] = px2.G / quality;
                            p2[2] = px2.B / quality;
                            sb.Append($"\x1b[48;2;");//háttér szín állítás ASCII escape-el
                            sb.Append(p2[0]* quality);
                            sb.Append(";");
                            sb.Append(p2[1] * quality);
                            sb.Append(";");
                            sb.Append(p2[2] * quality);
                            sb.Append("m");
                        }
                        sb.Append((char)223);//"▄"
                    }
                }
            }
            sb.Append("\x1b[0m");//reset ASCII kód, hogy a lejátszó alatti csík ne legyen majd színes
            return sb.ToString();
        }
    }
}
