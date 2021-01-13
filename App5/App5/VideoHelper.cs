using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor.Formats.QuickTime;

namespace App5
{
    public static class VideoHelper
    {
        #region Constants and Fields
        #endregion

        #region Constructors

        static VideoHelper()
        {
        }

        #endregion

        #region Public Methods

        public static TimeSpan GetVideDuration(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var exifDirectories = QuickTimeMetadataReader.ReadMetadata(stream);
            if (exifDirectories.Count == 0)
                return TimeSpan.MinValue;

            TimeSpan duration = TimeSpan.MinValue;
            foreach (var exifDir in exifDirectories)
            {
                System.Diagnostics.Debug.WriteLine($"EXIF Dir '{exifDir.Name}'");
                foreach(var exifTag in exifDir.Tags)
                    System.Diagnostics.Debug.WriteLine($"\tEXIF Tag '{exifTag.DirectoryName}\\{exifTag.Name}'");

                var exifTagDuration = exifDir.Tags.FirstOrDefault(x => x.Name.Contains("Duration"));
                if (exifTagDuration != null)
                {
                    duration = TimeSpan.Parse(exifTagDuration.Description);
                    //break;
                }
            }

            if (duration == TimeSpan.MinValue)
                return TimeSpan.MinValue;

            return duration;
        }

        public static IReadOnlyList<MetadataExtractor.Directory> GetEXIFData(Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var exifData = QuickTimeMetadataReader.ReadMetadata(stream);
            return exifData;
        }

        public static MetadataExtractor.Tag GetEXIFTag(IReadOnlyList<MetadataExtractor.Directory> exifData, string tagName)
        {
            if (exifData.Count == 0)
                return null;

            foreach (var exifDir in exifData)
            {
                foreach (var exifTag in exifDir.Tags)
                {
                    if (exifTag.Name.ToLower() == tagName.ToLower())
                        return exifTag;
                }
            }

            return null;
        }

        public static MetadataExtractor.Tag[] GetEXIFTags(IReadOnlyList<MetadataExtractor.Directory> exifData, string tagName)
        {
            if (exifData.Count == 0)
                return null;

            List<MetadataExtractor.Tag> tags = new List<MetadataExtractor.Tag>();
            foreach (var exifDir in exifData)
            {
                foreach (var exifTag in exifDir.Tags)
                {
                    if (exifTag.Name.ToLower() == tagName.ToLower())
                        tags.Add(exifTag);
                }
            }

            return tags.ToArray();
        }

        #endregion
    }
}
