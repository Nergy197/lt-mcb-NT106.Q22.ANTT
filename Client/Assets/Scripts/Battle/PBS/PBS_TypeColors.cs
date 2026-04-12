using System.Collections.Generic;
using UnityEngine;

namespace PokemonMMO.UI.Battle
{
    public static class PBS_TypeColors
    {
        public static readonly Dictionary<string, Color> TypeColors = new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["normal"]   = new Color(0.66f, 0.65f, 0.47f),
            ["fire"]     = new Color(0.94f, 0.50f, 0.19f),
            ["water"]    = new Color(0.41f, 0.56f, 0.94f),
            ["electric"] = new Color(0.98f, 0.83f, 0.20f),
            ["grass"]    = new Color(0.47f, 0.73f, 0.29f),
            ["ice"]      = new Color(0.60f, 0.85f, 0.85f),
            ["fighting"] = new Color(0.75f, 0.24f, 0.17f),
            ["poison"]   = new Color(0.63f, 0.27f, 0.63f),
            ["ground"]   = new Color(0.88f, 0.75f, 0.41f),
            ["flying"]   = new Color(0.67f, 0.56f, 0.94f),
            ["psychic"]  = new Color(0.97f, 0.35f, 0.53f),
            ["bug"]      = new Color(0.66f, 0.73f, 0.09f),
            ["rock"]     = new Color(0.71f, 0.63f, 0.28f),
            ["ghost"]    = new Color(0.44f, 0.35f, 0.59f),
            ["dragon"]   = new Color(0.44f, 0.25f, 0.94f),
            ["dark"]     = new Color(0.44f, 0.35f, 0.29f),
            ["steel"]    = new Color(0.72f, 0.72f, 0.81f),
            ["fairy"]    = new Color(0.99f, 0.62f, 0.99f),
            ["unknown"]  = new Color(0.5f,  0.5f,  0.5f)
        };

        public static Color GetTypeColor(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return TypeColors["unknown"];
            return TypeColors.TryGetValue(typeName, out var color) ? color : TypeColors["unknown"];
        }
    }
}
