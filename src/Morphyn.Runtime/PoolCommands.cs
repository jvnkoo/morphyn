using System;
using System.Linq;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    internal static class PoolCommands
    {
        public static bool HandlePoolCommand(MorphynPool pool, string command, MorphynValue[] args, EntityData data)
        {
            switch (command)
            {
                case "add":
                    if (args.Length == 0 || args[0].Kind == MorphynValueKind.Null)
                        throw new Exception("Pool add command requires a non-null argument");
                    string typeName = args[0].ToString()!;
                    if (data.Entities.TryGetValue(typeName, out var prototype))
                    {
                        var newEntity = prototype.Clone();
                        pool.Values.Add(newEntity);
                        MorphynRuntime.Send(newEntity, "init");
                    }
                    else
                    {
                        pool.Values.Add(args[0]);
                    }
                    return true;
                case "push":
                    pool.Values.Insert(0, args[0]);
                    return true;
                case "insert":
                    if (args[0].Kind == MorphynValueKind.Null) throw new Exception("Insert index cannot be null");
                    pool.Values.Insert(Convert.ToInt32(args[0]) - 1, args[1]);
                    return true;
                case "remove_at":
                    if (args[0].Kind == MorphynValueKind.Null) throw new Exception("Remove_at index cannot be null");
                    int idxRem = Convert.ToInt32(args[0]) - 1;
                    if (idxRem >= 0 && idxRem < pool.Values.Count) pool.Values.RemoveAt(idxRem);
                    return true;
                case "remove":
                    pool.Values.Remove(args[0]);
                    return true;
                case "pop":
                    if (pool.Values.Count > 0) pool.Values.RemoveAt(pool.Values.Count - 1);
                    return true;
                case "shift":
                    if (pool.Values.Count > 0) pool.Values.RemoveAt(0);
                    return true;
                case "swap":
                    if (args[0].Kind == MorphynValueKind.Null || args[1].Kind == MorphynValueKind.Null) throw new Exception("Swap indices cannot be null");
                    int i1 = Convert.ToInt32(args[0]) - 1;
                    int i2 = Convert.ToInt32(args[1]) - 1;
                    if (i1 >= 0 && i1 < pool.Values.Count && i2 >= 0 && i2 < pool.Values.Count)
                    {
                        var temp = pool.Values[i1];
                        pool.Values[i1] = pool.Values[i2];
                        pool.Values[i2] = temp;
                    }
                    return true;
                case "clear":
                    pool.Values.Clear();
                    return true;
                case "sort":
                    pool.Values.Sort();
                    return true;
                case "reverse":
                    pool.Values.Reverse();
                    return true;
                case "contains":
                    return pool.Values.Contains(args[0]);
                case "shuffle":
                    pool.Values = pool.Values.OrderBy(x => Guid.NewGuid()).ToList();
                    return true;
                default:
                    return false;
            }
        }
    }
}