namespace CustomTvVideos
{
#nullable enable
    internal interface ITryGet<T>
    {
        bool TryGetValue(out T? value);
    }
#nullable restore
}
