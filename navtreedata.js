/*
 @licstart  The following is the entire license notice for the JavaScript code in this file.

 The MIT License (MIT)

 Copyright (C) 1997-2020 by Dimitri van Heesch

 Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 and associated documentation files (the "Software"), to deal in the Software without restriction,
 including without limitation the rights to use, copy, modify, merge, publish, distribute,
 sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in all copies or
 substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
 BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

 @licend  The above is the entire license notice for the JavaScript code in this file
*/
var NAVTREE =
[
  [ "Morphyn Language", "index.html", [
    [ "Morphyn Language Documentation", "index.html", "index" ],
    [ "Getting Started with Morphyn", "getting_started.html", [
      [ "Usage", "getting_started.html#usage", null ],
      [ "Supported File Extensions", "getting_started.html#file_extensions", [
        [ "Runtime Features", "getting_started.html#features", null ],
        [ "Hot Reload", "getting_started.html#hot_reload", null ],
        [ "Tick System", "getting_started.html#tick_system", null ],
        [ "Init Event", "getting_started.html#init_event", null ]
      ] ]
    ] ],
    [ "Import System", "imports.html", [
      [ "Import Syntax", "imports.html#import_syntax", null ],
      [ "Import Rules", "imports.html#import_rules", null ]
    ] ],
    [ "Language Syntax Reference", "language_syntax.html", [
      [ "Overview", "language_syntax.html#syntax_overview", null ],
      [ "Comments", "language_syntax.html#syntax_comments", null ],
      [ "Entity Declaration", "language_syntax.html#syntax_entities", null ],
      [ "Field Declaration", "language_syntax.html#syntax_fields", null ],
      [ "Event Handlers", "language_syntax.html#syntax_events", null ],
      [ "Actions", "language_syntax.html#syntax_actions", [
        [ "Data Flow (Arrow)", "language_syntax.html#syntax_flow", null ],
        [ "Check (Guard)", "language_syntax.html#syntax_check", null ],
        [ "Emit (Event Dispatch)", "language_syntax.html#syntax_emit", null ],
        [ "Block Actions", "language_syntax.html#syntax_block", null ]
      ] ],
      [ "Operators", "language_syntax.html#syntax_operators", [
        [ "Arithmetic", "language_syntax.html#syntax_arithmetic", null ],
        [ "Comparison", "language_syntax.html#syntax_comparison", null ],
        [ "Logic", "language_syntax.html#syntax_logic", null ],
        [ "Flow", "language_syntax.html#syntax_flow_op", null ]
      ] ],
      [ "Keywords", "language_syntax.html#syntax_keywords", null ]
    ] ],
    [ "Expression System", "expressions.html", [
      [ "Expression Types", "expressions.html#expr_types", [
        [ "Literals", "expressions.html#literals", null ],
        [ "Variables", "expressions.html#variables", null ],
        [ "Arithmetic Operators", "expressions.html#arithmetic", null ],
        [ "Comparison Operators", "expressions.html#comparison", null ],
        [ "Logic Operators", "expressions.html#logic", null ]
      ] ],
      [ "Pool Access", "expressions.html#pool_access", null ]
    ] ],
    [ "Event System", "event_system.html", [
      [ "Overview", "event_system.html#event_overview", null ],
      [ "Built-in Events", "event_system.html#builtin_events", [
        [ "tick(dt)", "event_system.html#tick_event", null ],
        [ "destroy", "event_system.html#destroy_event", null ]
      ] ],
      [ "Custom Events", "event_system.html#custom_events", null ]
    ] ],
    [ "Pool System", "pools.html", [
      [ "Overview", "pools.html#pool_overview", null ],
      [ "Declaration", "pools.html#pool_declaration", null ],
      [ "Pool Commands", "pools.html#pool_commands", [
        [ "Adding Elements", "pools.html#pool_add", null ],
        [ "Removing Elements", "pools.html#pool_remove", null ],
        [ "Other Operations", "pools.html#pool_other", null ]
      ] ]
    ] ],
    [ "Example Programs", "examples.html", [
      [ "Simple Counter", "examples.html#example_simple", null ],
      [ "Health System", "examples.html#example_health", null ],
      [ "Enemy AI", "examples.html#example_enemy", null ],
      [ "Inventory System", "examples.html#example_inventory", null ],
      [ "Complete Game Example", "examples.html#example_game", null ]
    ] ],
    [ "Unity Integration", "unity_integration.html", [
      [ "Overview", "unity_integration.html#unity_overview", null ],
      [ "Installation", "unity_integration.html#unity_installation", null ],
      [ "JSON vs Morphyn", "unity_integration.html#unity_comparison", [
        [ "The JSON Way (BAD)", "unity_integration.html#unity_json_example", null ],
        [ "The Morphyn Way (GOOD)", "unity_integration.html#unity_morphyn_example", null ]
      ] ],
      [ "Quick Start Example", "unity_integration.html#unity_quickstart", [
        [ "Step 1: Create Config File", "unity_integration.html#unity_step1", null ],
        [ "Step 2: Read Values from Unity", "unity_integration.html#unity_step2", null ]
      ] ],
      [ "MorphynController", "unity_integration.html#unity_controller", [
        [ "Settings", "unity_integration.html#unity_controller_settings", null ],
        [ "API", "unity_integration.html#unity_controller_api", null ]
      ] ],
      [ "Unity Bridge", "unity_integration.html#unity_bridge", [
        [ "Setup", "unity_integration.html#unity_bridge_setup", null ],
        [ "Calling from Morphyn", "unity_integration.html#unity_bridge_call", null ],
        [ "Built-in Callbacks", "unity_integration.html#unity_bridge_builtin", null ]
      ] ],
      [ "Real Examples", "unity_integration.html#unity_examples", [
        [ "Shop with Discount Logic", "unity_integration.html#unity_example_shop", null ],
        [ "Inventory with Capacity", "unity_integration.html#unity_example_inventory", null ],
        [ "Enemy Spawner with Timer", "unity_integration.html#unity_example_spawner", null ]
      ] ],
      [ "Hot Reload", "unity_integration.html#unity_hotreload", null ],
      [ "Save/Load", "unity_integration.html#unity_save", null ],
      [ "Best Practices", "unity_integration.html#unity_best_practices", null ],
      [ "Common Issues", "unity_integration.html#unity_troubleshooting", null ]
    ] ],
    [ "Visual Studio Extension", "vsix_extension.html", [
      [ "Overview", "vsix_extension.html#vsix_overview", null ],
      [ "Installation", "vsix_extension.html#vsix_install", null ],
      [ "Features", "vsix_extension.html#vsix_features", null ]
    ] ],
    [ "Topics", "topics.html", "topics" ],
    [ "Packages", "namespaces.html", [
      [ "Package List", "namespaces.html", "namespaces_dup" ],
      [ "Package Members", "namespacemembers.html", [
        [ "All", "namespacemembers.html", null ],
        [ "Functions", "namespacemembers_func.html", null ],
        [ "Enumerations", "namespacemembers_enum.html", null ]
      ] ]
    ] ],
    [ "Classes", "annotated.html", [
      [ "Class List", "annotated.html", "annotated_dup" ],
      [ "Class Index", "classes.html", null ],
      [ "Class Hierarchy", "hierarchy.html", "hierarchy" ],
      [ "Class Members", "functions.html", [
        [ "All", "functions.html", null ],
        [ "Functions", "functions_func.html", null ],
        [ "Variables", "functions_vars.html", null ],
        [ "Properties", "functions_prop.html", null ]
      ] ]
    ] ],
    [ "Files", "files.html", [
      [ "File List", "files.html", "files_dup" ]
    ] ]
  ] ]
];

var NAVTREEINDEX =
[
"EntityData_8cs.html",
"functions_func.html"
];

var SYNCONMSG = 'click to disable panel synchronisation';
var SYNCOFFMSG = 'click to enable panel synchronisation';