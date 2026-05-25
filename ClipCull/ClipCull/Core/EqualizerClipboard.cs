using ClipCull.Models;

namespace ClipCull.Core
{
    /// <summary>
    /// Process-wide clipboard for equalizer settings, used by the copy/paste
    /// buttons on the equalizer panel.
    /// </summary>
    public static class EqualizerClipboard
    {
        private static EqualizerSettings _stored;

        public static bool HasContent => _stored != null;

        public static void Copy(EqualizerSettings source)
        {
            if (source == null)
            {
                _stored = null;
                return;
            }
            _stored = Clone(source);
        }

        /// <summary>
        /// Copies the stored values into <paramref name="target"/>, in place,
        /// so existing bindings remain attached to the same instance.
        /// Returns false if there is nothing on the clipboard.
        /// </summary>
        public static bool PasteInto(EqualizerSettings target)
        {
            if (target == null || _stored == null)
                return false;

            target.Enabled = _stored.Enabled;
            target.PreampDb = _stored.PreampDb;

            var bands = new double[EqualizerSettings.BandCount];
            if (_stored.BandGainsDb != null)
            {
                int copyLen = System.Math.Min(_stored.BandGainsDb.Length, bands.Length);
                System.Array.Copy(_stored.BandGainsDb, bands, copyLen);
            }
            target.BandGainsDb = bands;
            return true;
        }

        public static EqualizerSettings Clone(EqualizerSettings source)
        {
            if (source == null) return null;
            var clone = new EqualizerSettings
            {
                Enabled = source.Enabled,
                PreampDb = source.PreampDb,
            };
            var bands = new double[EqualizerSettings.BandCount];
            if (source.BandGainsDb != null)
            {
                int copyLen = System.Math.Min(source.BandGainsDb.Length, bands.Length);
                System.Array.Copy(source.BandGainsDb, bands, copyLen);
            }
            clone.BandGainsDb = bands;
            return clone;
        }
    }
}
