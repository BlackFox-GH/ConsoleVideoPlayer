using System.Drawing;
using System.Text;

namespace ConsoleVideoPlayer.ConsoleRenderModes
{
    internal class ConsoleFrameRendererImpl_2Color : IConsoleFrameRenderer//fekete-fehér megjelenítő mód, 4 karakter használatával
    {
        public ConsoleFrameRendererImpl_2Color()
        {
            this.preferredPixelCount = PreferredPixelCount.TWO_PER_CHAR;//itt két pixelt kell néznünk
        }

        public override string getFrameText(Bitmap frame)
        {
            StringBuilder sb = new StringBuilder();
            lock (frame)
            {
                for (int y = 1; y < frame.Height; y += 2)
                {
                    for (int x = 0; x < frame.Width; x++)
                    {
                        bool p1 = true, p2 = true;
                        double B = frame.GetPixel(x, y - 1).GetBrightness();//a pixel világosságát kérdezzük le
                        if (B < 0.5)//megnézzük hogy sötét-e (brightness<0.5)
                        {
                            p2 = false;
                        }
                        double F = frame.GetPixel(x, y).GetBrightness();//a pixel világosságát kérdezzük le
                        if (F < 0.5)//megnézzük hogy sötét-e (brightness<0.5)
                        {
                            p1 = false;
                        }
                        //A két pixel alapján meghatározzuk a kiírandó karaktert
                        if (p1 == false && p2 == false) sb.Append(" ");//" "
                        else if (p1 && p2) sb.Append((char)368);//"█"
                        else if (p1 == false && p2) sb.Append((char)223);//"▄"
                        else sb.Append((char)220);//"▀"
                    }
                }
            }
            return sb.ToString();
        }
    }
}
