using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Morphyn.Parser;
using Morphyn.Runtime;
using UnityEngine;

namespace Morphyn.Unity
{
    /// <summary>
    /// Handles serialization and deserialization of Morphyn entities
    /// Saves entities as human-readable .morphyn files
    /// </summary>
    public static class MorphynSerializer
    {
        /// <summary>
        /// Save a single entity to a .morphyn file
        /// Only saves field values, not event handlers
        /// </summary>
        /// <param name="entity">Entity to save</param>
        /// <param name="filePath">Full path to save file</param>
        public static void SaveEntity(Entity entity, string filePath)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"entity {entity.Name} {{");
            
            // Save all fields
            foreach (var field in entity.Fields)
            {
                string value = FormatValue(field.Value);
                sb.AppendLine($"  has {field.Key}: {value}");
            }
            
            sb.AppendLine("}");
            
            File.WriteAllText(filePath, sb.ToString());
        }
        
        /// <summary>
        /// Save all entities from EntityData to separate files
        /// Creates folder if it doesn't exist
        /// </summary>
        /// <param name="data">EntityData containing all entities</param>
        /// <param name="folderPath">Folder to save entity files</param>
        public static void SaveAllEntities(EntityData data, string folderPath)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            
            foreach (var entity in data.Entities.Values)
            {
                string filePath = Path.Combine(folderPath, $"{entity.Name}.morphyn");
                SaveEntity(entity, filePath);
            }
            
            Debug.Log($"[Morphyn] Saved {data.Entities.Count} entities to {folderPath}");
        }
        
        /// <summary>
        /// Load entity fields from a .morphyn file
        /// Only loads field values, event handlers are not modified
        /// </summary>
        /// <param name="target">Existing entity to update with loaded fields</param>
        /// <param name="filePath">Path to .morphyn file to load</param>
        public static void LoadEntityFields(Entity target, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[Morphyn] File not found: {filePath}");
                return;
            }
            
            string code = File.ReadAllText(filePath);
            var loaded = MorphynParser.ParseFile(code);
            
            if (loaded.Entities.Count > 0)
            {
                var source = loaded.Entities.Values.First();
                
                // Copy fields from loaded entity to target
                foreach (var field in source.Fields)
                {
                    target.Fields[field.Key] = field.Value;
                }
                
                Debug.Log($"[Morphyn] Loaded fields for {target.Name} from {filePath}");
            }
        }
        
        /// <summary>
        /// Format a value for .morphyn file output
        /// Handles null, strings, bools, numbers, and pools
        /// </summary>
        private static string FormatValue(object? value)
        {
            return value switch
            {
                null => "null",
                string s => $"\"{s}\"",
                bool b => b.ToString().ToLower(),
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                MorphynPool p => FormatPool(p),
                _ => value.ToString() ?? "null"
            };
        }
        
        /// <summary>
        /// Format a MorphynPool for .morphyn file output
        /// Example: pool["item1", "item2", 42]
        /// </summary>
        private static string FormatPool(MorphynPool pool)
        {
            var items = pool.Values.Select(v => FormatValue(v));
            return $"pool[{string.Join(", ", items)}]";
        }
    }
}