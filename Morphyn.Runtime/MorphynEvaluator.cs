/**
 * \file MorphynEvaluator.cs
 * \brief Expression evaluation engine
 * \defgroup evaluator Expression Evaluator
 * @{
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using Morphyn.Parser;

namespace Morphyn.Runtime
{
    /**
     * \class MorphynEvaluator
     * \brief Evaluates Morphyn expressions
     * 
     * \page expressions Expression System
     * 
     * \section expr_types Expression Types
     * 
     * \subsection literals Literals
     * 
     * \par Numbers
     * \code{.morphyn}
     * 100           # Integer
     * 3.14          # Floating point
     * -42           # Negative
     * \endcode
     * 
     * \par Strings
     * \code{.morphyn}
     * "Hello"
     * "Player Name"
     * \endcode
     * 
     * \par Booleans
     * \code{.morphyn}
     * true
     * false
     * \endcode
     * 
     * \subsection variables Variables
     * 
     * \par Entity Fields
     * \code{.morphyn}
     * hp
     * name
     * alive
     * \endcode
     * 
     * \par Event Parameters
     * \code{.morphyn}
     * on damage(amount) {
     *   hp - amount -> hp  # 'amount' is a parameter
     * }
     * \endcode
     * 
     * \subsection arithmetic Arithmetic Operators
     * 
     * \par Basic Math
     * \code{.morphyn}
     * hp + 10       # Addition
     * hp - 5        # Subtraction
     * damage * 2    # Multiplication
     * armor / 3     # Division
     * level % 5     # Modulo
     * \endcode
     * 
     * \par Complex Expressions
     * \code{.morphyn}
     * (hp + shield) * 0.5 -> total_defense
     * damage * (1 - armor / 100) -> final_damage
     * \endcode
     * 
     * \subsection comparison Comparison Operators
     * 
     * \code{.morphyn}
     * hp > 0           # Greater than
     * level >= 10      # Greater or equal
     * hp < max_hp      # Less than
     * mana <= 0        # Less or equal
     * state == "idle"  # Equal
     * hp != 0          # Not equal
     * \endcode
     * 
     * \subsection logic Logic Operators
     * 
     * \par AND
     * \code{.morphyn}
     * check hp > 0 and mana > 10: emit cast_spell
     * \endcode
     * 
     * \par OR
     * \code{.morphyn}
     * check state == "idle" or state == "walk": emit can_interact
     * \endcode
     * 
     * \par NOT
     * \code{.morphyn}
     * check not dead: emit move
     * check not (hp < 10 and mana < 5): emit safe_to_fight
     * \endcode
     * 
     * \subsection pool_access Pool Access
     * 
     * \par Get Pool Size
     * \code{.morphyn}
     * enemies.count -> num_enemies
     * \endcode
     * 
     * \par Access by Index (1-based)
     * \code{.morphyn}
     * enemies.at[1] -> first_enemy
     * items.at[i] -> current_item
     * \endcode
     * 
     * \par Access Entity Fields
     * \code{.morphyn}
     * player.hp -> player_health
     * enemy.damage -> incoming_damage
     * \endcode
     */
    public static class MorphynEvaluator
    {
        public static object? EvaluateExpression(Entity entity, MorphynExpression expr, 
            Dictionary<string, object?> localScope, EntityData data)
        {
            return expr switch
            {
                LiteralExpression l => l.Value,

                VariableExpression v => 
                    localScope.TryGetValue(v.Name, out var argVal) ? argVal :
                        (entity.Fields.TryGetValue(v.Name, out var fieldVal) 
                            ? fieldVal 
                            : throw new Exception($"Variable '{v.Name}' not found in '{entity.Name}'")),

                BinaryExpression b => EvaluateBinary(entity, b, localScope, data), 

                BinaryLogicExpression bl => EvaluateLogic(entity, bl, localScope, data),
                
                UnaryLogicExpression u => EvaluateUnary(entity, u, localScope, data), 

                IndexAccessExpression idx => GetFromPool(entity, idx, localScope, data), 
        
                PoolPropertyExpression p => GetPoolProperty(entity, p, data), 

                _ => throw new Exception($"Unsupported expression: {expr.GetType().Name}")
            };
        }

        private static bool EvaluateLogic(Entity entity, BinaryLogicExpression b, Dictionary<string, object?> localScope, EntityData data)
        {
            var left = EvaluateExpression(entity, b.Left, localScope, data);
            if (left == null) return false;
            
            bool leftBool = (bool)left;
            
            if (b.Operator == "or") 
            {
                if (leftBool) return true;
                var right = EvaluateExpression(entity, b.Right, localScope, data);
                return right != null && (bool)right;
            }
            
            if (b.Operator == "and") 
            {
                if (!leftBool) return false;
                var right = EvaluateExpression(entity, b.Right, localScope, data);
                return right != null && (bool)right;
            }
            
            throw new Exception($"Unknown logic operator: {b.Operator}");
        }

        private static bool EvaluateUnary(Entity entity, UnaryLogicExpression u, Dictionary<string, object?> localScope, EntityData data)
        {
            var val = EvaluateExpression(entity, u.Inner, localScope, data);
            if (val == null) return true;
            return !(bool)val;
        }

        private static object? GetPoolProperty(Entity entity, PoolPropertyExpression p, EntityData data)
        {
            if (entity.Fields.TryGetValue(p.TargetName, out var obj) && obj is MorphynPool pool)
            {
                if (p.Property == "count") return (double)pool.Values.Count;
                throw new Exception($"Property '{p.Property}' not supported for pools.");
            }

            if (data.Entities.TryGetValue(p.TargetName, out var externalEntity))
            {
                if (externalEntity.Fields.TryGetValue(p.Property, out var val))
                {
                    return val;
                }
                throw new Exception($"Field '{p.Property}' not found in external entity '{p.TargetName}'");
            }

            throw new Exception($"Entity or Pool '{p.TargetName}' not found.");
        }

        private static object? GetFromPool(Entity entity, IndexAccessExpression idx, Dictionary<string, object?> localScope, EntityData data)
        {
            if (entity.Fields.TryGetValue(idx.TargetName, out var val) && val is MorphynPool pool)
            {
                var evalIndex = EvaluateExpression(entity, idx.IndexExpr, localScope, data);
                if (evalIndex == null)
                    throw new Exception($"Index expression evaluated to null for pool '{idx.TargetName}'");
                
                int index = Convert.ToInt32(evalIndex) - 1; 
        
                if (index >= 0 && index < pool.Values.Count)
                    return pool.Values[index];
            
                throw new Exception($"Runtime Error: Index {index + 1} is out of bounds for pool '{idx.TargetName}'");
            }
            throw new Exception($"Target '{idx.TargetName}' is not a pool.");
        }

        private static object EvaluateBinary(Entity entity, BinaryExpression b, Dictionary<string, object?> localScope, EntityData data)
        {
            var leftObj = EvaluateExpression(entity, b.Left, localScope, data);
            var rightObj = EvaluateExpression(entity, b.Right, localScope, data);

            // Handle null comparisons for equality operators
            if (b.Operator == "==" || b.Operator == "!=")
            {
                if (leftObj == null || rightObj == null)
                {
                    return b.Operator == "==" ? Equals(leftObj, rightObj) : !Equals(leftObj, rightObj);
                }
            }
            else
            {
                // For non-equality operators, null is not allowed
                if (leftObj == null || rightObj == null)
                {
                    throw new Exception($"Cannot perform operation '{b.Operator}' with null operand");
                }
            }

            if (IsNumeric(leftObj) && IsNumeric(rightObj))
            {
                double l = Convert.ToDouble(leftObj, CultureInfo.InvariantCulture);
                double r = Convert.ToDouble(rightObj, CultureInfo.InvariantCulture);

                return b.Operator switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => Math.Abs(r) > 1e-9 ? l / r : 0.0,
                    "%" => l % r,
                    ">" => l > r,
                    "<" => l < r,
                    ">=" => l >= r,
                    "<=" => l <= r,
                    "==" => Math.Abs(l - r) < 1e-7,
                    "!=" => Math.Abs(l - r) > 1e-7,
                    _ => throw new Exception($"Unknown operator: {b.Operator}")
                };
            }

            return b.Operator switch
            {
                "==" => Equals(leftObj, rightObj),
                "!=" => !Equals(leftObj, rightObj),
                _ => throw new Exception($"Operator {b.Operator} not supported for these types")
            };
        }

        private static bool IsNumeric(object? obj) => 
            obj is sbyte || obj is byte || obj is short || obj is ushort || 
            obj is int || obj is uint || obj is long || obj is ulong || 
            obj is float || obj is double || obj is decimal;
    }
}
/** @} */ // end of evaluator group