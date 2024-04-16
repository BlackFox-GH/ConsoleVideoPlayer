using System.Drawing;

namespace ConsoleVideoPlayer.ConsoleRenderModes
{
    public abstract class IConsoleFrameRenderer//a parancssoros képkocka feldolgozó absztrakt osztálya
    {
        public PreferredPixelCount preferredPixelCount;
        public abstract string getFrameText(Bitmap frame);

        public PreferredPixelCount GetPreferredPixelCount()
        {
            return preferredPixelCount;
        }
    }
}
