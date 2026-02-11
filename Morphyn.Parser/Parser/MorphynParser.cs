/**
 * \file MorphynParser.cs
 * \brief Morphyn language parser
 * \defgroup parser Parser
 * @{
 */
using System;
using System.Collections.Generic;
using System.Linq;
using Superpower;
using Superpower.Parsers;

namespace Morphyn.Parser
{
    using System.Globalization;

    /**
     * \page language_syntax Language Syntax Reference
     * 
     * \tableofcontents
     * 
     * \section syntax_overview Overview
     * 
     * Morphyn is a declarative language with minimal syntax. Programs consist
     * of entity declarations with fields and event handlers.
     * 
     * \section syntax_comments Comments
     * 
     * Three comment styles are supported:
     * 
     * \code{.morphyn}
     * # Single-line comment (shell style)
     * // Single-line comment (C++ style)
     * \endcode
     * 
     * Multi-line C-style comments are also supported.
     * 
     * \section syntax_entities Entity Declaration
     * 
     * \code{.morphyn}
     * entity EntityName {
     *   # Fields and events
     * }
     * \endcode
     * 
     * \section syntax_fields Field Declaration
     * 
     * \par Basic Fields
     * \code{.morphyn}
     * has field_name: value
     * has hp: 100
     * has name: "Player"
     * has alive: true
     * has exist : null
     * \endcode
     * 
     * \par Pool Fields
     * \code{.morphyn}
     * has items: pool[1, 2, 3]
     * has names: pool["Alice", "Bob"]
     * has flags: pool[true, false, true]
     * \endcode
     * 
     * \section syntax_events Event Handlers
     * 
     * \par Without Parameters
     * \code{.morphyn}
     * on event_name {
     *   # actions
     * }
     * \endcode
     * 
     * \par With Parameters
     * \code{.morphyn}
     * on event_name(param1, param2) {
     *   # actions
     * }
     * \endcode
     * 
     * \section syntax_actions Actions
     * 
     * \subsection syntax_flow Data Flow (Arrow)
     * 
     * \code{.morphyn}
     * expression -> target
     * hp - 10 -> hp
     * damage * 2 -> result
     * 0 -> counter
     * \endcode
     * 
     * \subsection syntax_check Check (Guard)
     * 
     * \code{.morphyn}
     * # Check with an inline action
     * check condition: action
     * check hp > 0: emit alive
     * check state == "idle": emit can_move
     *
     * # Check without an inline action (guard)
     * # If false, the event execution is stopped
     * check i < 0
     * \endcode
     * 
     * \subsection syntax_emit Emit (Event Dispatch)
     * 
     * \code{.morphyn}
     * emit event_name
     * emit event_name(arg1, arg2)
     * emit target.event_name
     * emit self.destroy
     * emit log("message", value)
     * \endcode
     * 
     * \subsection syntax_block Block Actions
     * 
     * \code{.morphyn}
     * {
     *   action1
     *   action2
     *   action3
     * }
     * \endcode
     * 
     * \section syntax_operators Operators
     * 
     * \subsection syntax_arithmetic Arithmetic
     * 
     * <table>
     * <tr><th>Operator</th><th>Description</th><th>Example</th></tr>
     * <tr><td>+</td><td>Addition</td><td>hp + 10</td></tr>
     * <tr><td>-</td><td>Subtraction</td><td>hp - 5</td></tr>
     * <tr><td>*</td><td>Multiplication</td><td>damage * 2</td></tr>
     * <tr><td>/</td><td>Division</td><td>armor / 3</td></tr>
     * <tr><td>%</td><td>Modulo</td><td>level % 5</td></tr>
     * </table>
     * 
     * \subsection syntax_comparison Comparison
     * 
     * <table>
     * <tr><th>Operator</th><th>Description</th><th>Example</th></tr>
     * <tr><td>==</td><td>Equal</td><td>hp == 100</td></tr>
     * <tr><td>!=</td><td>Not equal</td><td>state != "dead"</td></tr>
     * <tr><td>&gt;</td><td>Greater than</td><td>hp &gt; 0</td></tr>
     * <tr><td>&lt;</td><td>Less than</td><td>hp &lt; max</td></tr>
     * <tr><td>&gt;=</td><td>Greater or equal</td><td>level &gt;= 10</td></tr>
     * <tr><td>&lt;=</td><td>Less or equal</td><td>mana &lt;= 0</td></tr>
     * </table>
     * 
     * \subsection syntax_logic Logic
     * 
     * <table>
     * <tr><th>Operator</th><th>Description</th><th>Example</th></tr>
     * <tr><td>and</td><td>Logical AND</td><td>hp &gt; 0 and alive</td></tr>
     * <tr><td>or</td><td>Logical OR</td><td>idle or walk</td></tr>
     * <tr><td>not</td><td>Logical NOT</td><td>not dead</td></tr>
     * </table>
     * 
     * \subsection syntax_flow_op Flow
     * 
     * <table>
     * <tr><th>Operator</th><th>Description</th><th>Example</th></tr>
     * <tr><td>-&gt;</td><td>Data flow</td><td>value -&gt; field</td></tr>
     * </table>
     * 
     * \section syntax_keywords Keywords
     * 
     * <table>
     * <tr><th>Keyword</th><th>Purpose</th></tr>
     * <tr><td>entity</td><td>Declare entity</td></tr>
     * <tr><td>has</td><td>Declare field</td></tr>
     * <tr><td>on</td><td>Declare event handler</td></tr>
     * <tr><td>emit</td><td>Send event</td></tr>
     * <tr><td>check</td><td>Conditional guard</td></tr>
     * <tr><td>pool</td><td>Collection type</td></tr>
     * <tr><td>true</td><td>Boolean true</td></tr>
     * <tr><td>false</td><td>Boolean false</td></tr>
     * </table>
     */
    public static partial class MorphynParser
    {
        private static readonly Tokenizer<MorphynToken> Tokenizer = MorphynTokenizer.Create();

        public static EntityData ParseFile(string input)
        {
            try
            {
                var tokens = Tokenizer.Tokenize(input);
                var result = RootParser.Parse(tokens);
                return new EntityData(result);
            }
            catch (ParseException ex)
            {
                Console.Error.WriteLine("Morphyn parse error");
                Console.Error.WriteLine($"Position: {ex.ErrorPosition}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                PrintErrorContext(input, ex.ErrorPosition);
                throw new Exception("Morphyn parsing failed. See context above.");
            }
        }

        private static void PrintErrorContext(string input, Superpower.Model.Position position)
        {
            var lines = input.Split('\n');
            int lineIndex = Math.Clamp(position.Line - 1, 0, lines.Length - 1);
            int columnIndex = Math.Clamp(position.Column - 1, 0, lines[lineIndex].Length);

            Console.Error.WriteLine("Context:");
            if (lineIndex < lines.Length)
            {
                Console.Error.WriteLine(lines[lineIndex]);
                Console.Error.WriteLine(new string(' ', columnIndex) + "^");
            }
        }
    }
}
/** @} */ // end of parser group