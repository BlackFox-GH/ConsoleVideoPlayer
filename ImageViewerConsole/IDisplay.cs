namespace ConsoleVideoPlayer
{
    internal interface IDisplay//a videót lejátszó class interfésze
    {
        bool isEnded { get; }
        bool isPlaying { get; }
        void increaseVolume(int amount);
        void decreaseVolume(int amount);
        void startPlay();
        void stopPlay();
    }
}
