namespace CustomTvVideos
{
    internal interface IGetter<out T>
    {
        public T GetValue();
    }
}
