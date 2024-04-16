using System.Drawing;
using System.Text;

namespace ConsoleVideoPlayer.ConsoleRenderModes
{
    internal class ConsoleFrameRendererImpl_Gradient : IConsoleFrameRenderer//színátmenetes megjelenítő mód, 5 karakter használatával
    {
        public override string getFrameText(Bitmap frame)
        {
            StringBuilder sb = new StringBuilder();
            lock (frame)
            {
                for (int y = 0; y < frame.Height; y++)
                {
                    for (int x = 0; x < frame.Width; x++)
                    {
                        double pixel = frame.GetPixel(x, y).GetBrightness();//a pixel világosságát nézzük, mivel szürkeárnyalatos
                        if (pixel <= 0.2) sb.Append(" ");//" "(szóköz)
                        else if (pixel <= 0.4) sb.Append((char)176);//"░"
                        else if (pixel <= 0.6) sb.Append((char)177);//"▒"
                        else if (pixel <= 0.8) sb.Append((char)731);//"▓"
                        else sb.Append((char)368);//"█"
                    }
                }
            }
            return sb.ToString();
        }
    }
}
