using System.Xml.Serialization;

namespace ClipCull.Models.Import
{
    /// <summary>
    /// Metadata describing a storage medium (SD card, SSD, ...). Written as <c>info.xml</c> at the
    /// root of a card so the importer can prefill the metadata form automatically. Values left empty
    /// are simply ignored during prefill.
    /// </summary>
    [XmlRoot("StorageInfo")]
    public class StorageInfo
    {
        /// <summary>Camera / device model, e.g. "GoPro HERO 12".</summary>
        public string CameraModel { get; set; }

        /// <summary>Default reel / card identifier.</summary>
        public string Reel { get; set; }

        /// <summary>Default author / operator.</summary>
        public string Author { get; set; }

        /// <summary>Default location.</summary>
        public string Location { get; set; }

        /// <summary>Free-form notes about the card. Not used for prefill.</summary>
        public string Notes { get; set; }

        public bool HasAnyValue()
        {
            return !string.IsNullOrWhiteSpace(CameraModel)
                || !string.IsNullOrWhiteSpace(Reel)
                || !string.IsNullOrWhiteSpace(Author)
                || !string.IsNullOrWhiteSpace(Location)
                || !string.IsNullOrWhiteSpace(Notes);
        }
    }
}
