from setuptools import setup

setup(
    name='morphyn-pygments-lexer',
    version='1.0.0',
    py_modules=['morphyn_lexer'],
    install_requires=['Pygments>=2.0'],
    entry_points={
        'pygments.lexers': [
            'morphyn = morphyn_lexer:MorphynLexer',
        ],
    },
)