from pygments.lexer import RegexLexer, bygroups, words
from pygments.token import *

class MorphynLexer(RegexLexer):
    name = 'Morphyn'
    aliases = ['morphyn', 'mrph']
    filenames = ['*.morphyn', '*.mrph', '*.morph']
    
    tokens = {
        'root': [
            # Comments
            (r'#.*$', Comment.Single),
            (r'//.*$', Comment.Single),
            (r'/\*', Comment.Multiline, 'comment'),
            
            # Strings
            (r'"[^"]*"', String.Double),
            
            # Keywords
            (words(('entity', 'on', 'has', 'check', 'import', 'emit'), suffix=r'\b'), Keyword),
            
            # Constants
            (words(('true', 'false', 'self', 'pool', 'null'), suffix=r'\b'), Keyword.Constant),
            
            # Built-in functions
            (words(('log', 'count', 'add', 'remove_at', 'at', 'each', 'push', 'pop', 'shift', 
                    'remove', 'insert', 'swap', 'clear', 'unity'), suffix=r'\b'), Name.Builtin),
            
            # Operators
            (r'->', Operator),
            (r'(==|!=|<=|>=|<|>)', Operator.Comparison),
            (r'(\+|\-|\*|/|%)', Operator.Arithmetic),
            (r'(and|or|not)', Operator.Word),
            
            # Numbers
            (r'\b\d+\.?\d*\b', Number),
            
            # Entity names (capitalized)
            (r'\b[A-Z][a-zA-Z0-9_]*\b', Name.Class),
            
            # Function/event names
            (r'(?<=on\s)[a-z_][a-zA-Z0-9_]*', Name.Function),
            
            # Field names
            (r'(?<=has\s)[a-z_][a-zA-Z0-9_]*', Name.Variable),
            
            # Identifiers
            (r'[a-z_][a-zA-Z0-9_]*', Name),
            
            # Punctuation
            (r'[{}()\[\]:,.]', Punctuation),
            
            # Whitespace
            (r'\s+', Text),
        ],
        'comment': [
            (r'[^*/]+', Comment.Multiline),
            (r'\*/', Comment.Multiline, '#pop'),
            (r'[*/]', Comment.Multiline),
        ],
    }