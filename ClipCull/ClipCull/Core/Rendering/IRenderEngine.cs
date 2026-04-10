using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClipCull.Core.Rendering
{
    public interface IRenderEngine
    {
        string Name { get; }

        RenderEngineType EngineType { get; }

        Task<bool> IsAvailableAsync();

        string GetStatusDescription();

        IReadOnlyList<VideoCodec> SupportedVideoCodecs { get; }
        IReadOnlyList<AudioCodec> SupportedAudioCodecs { get; }
        IReadOnlyList<ContainerFormat> SupportedContainerFormats { get; }
        IReadOnlyList<HardwareAcceleration> SupportedHardwareAcceleration { get; }

        Task<string> RenderAsync(
            RenderJobInfo job,
            RenderSettings settings,
            string outputDirectory,
            bool overwrite,
            Action<RenderProgress> progressCallback,
            CancellationToken cancellationToken = default);
    }
}
