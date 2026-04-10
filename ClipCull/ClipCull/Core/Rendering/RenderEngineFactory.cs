using ClipCull.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClipCull.Core.Rendering
{
    public static class RenderEngineFactory
    {
        private static readonly Dictionary<RenderEngineType, IRenderEngine> _engines = new();

        public static void Register(IRenderEngine engine)
        {
            _engines[engine.EngineType] = engine;
        }

        public static IRenderEngine Create(RenderEngineType type)
        {
            if (_engines.TryGetValue(type, out var engine))
                return engine;

            throw new ArgumentException($"No render engine registered for type: {type}");
        }

        public static IReadOnlyList<IRenderEngine> GetAllEngines()
        {
            return _engines.Values.ToList().AsReadOnly();
        }

        public static async Task<IReadOnlyList<IRenderEngine>> GetAvailableEnginesAsync()
        {
            var available = new List<IRenderEngine>();
            foreach (var engine in _engines.Values)
            {
                if (await engine.IsAvailableAsync())
                    available.Add(engine);
            }
            return available.AsReadOnly();
        }
    }
}
