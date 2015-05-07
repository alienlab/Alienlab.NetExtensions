namespace Alienlab.NetExtensions
{
  using System.Collections.Generic;

  public interface IBlockOptions
  {
    IEnumerable<DownloadContext.Block> GetBlocks(Size fileSize);
  }
}