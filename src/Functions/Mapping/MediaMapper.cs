using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alejof.Notes.Functions.TableStorage;
using Humanizer;

namespace Alejof.Notes.Functions.Mapping
{
    public static class MediaMapper
    {
        public static string AsMediaName(this string name) => 
            $"{System.IO.Path.GetFileNameWithoutExtension(name)}-{DateTime.UtcNow.ToString("yyMMddhhmmss")}{System.IO.Path.GetExtension(name)}";

        public static Models.Media ToMediaModel(this MediaEntity entity) =>
            new Models.Media
            {
                Id = entity.RowKey,
                Name = entity.Name,
                BlobUri = entity.BlobUri,
            };
    }
}
