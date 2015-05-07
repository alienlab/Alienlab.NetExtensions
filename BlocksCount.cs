namespace Alienlab.NetExtensions
{
  using System.Collections.Generic;

  public class BlocksCount : IBlockOptions
  {
    public int Count { get; }

    public BlocksCount(int count)
    {
      this.Count = count;
    }

    public IEnumerable<DownloadContext.Block> GetBlocks(Size fileSize)
    {
      var index = 0;
      var offset = 0L;
      var count = this.Count;
      var bytesLeft = fileSize.Bytes;
      var maxBlockSize = Size.FromBytes(bytesLeft / count + count);
      while (bytesLeft >= maxBlockSize)
      {
        yield return new DownloadContext.Block(index++, offset, maxBlockSize);

        offset += maxBlockSize;
        bytesLeft -= maxBlockSize;
      }

      yield return new DownloadContext.Block(index, offset, Size.FromBytes(bytesLeft));
    }
  }
}