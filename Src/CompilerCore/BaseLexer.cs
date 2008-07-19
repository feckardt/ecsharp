using System.IO;
using System.Collections.Generic;
using Loyc.Runtime;
using Loyc.Utilities;
using System;
using System.Diagnostics;

namespace Loyc.CompilerCore
{
	/// <summary>An appropriate base class for Loyc lexers. This serves as 
	/// the base class for Loyc's boo-style lexer and C#-style lexer.
	/// </summary>
	public abstract class BaseLexer : BaseRecognizer<int>, IEnumerable<AstNode>, IParseNext<AstNode>
	{
		protected ISourceFile _source2;
		protected int _startingPosition;
		protected Symbol _nodeType;
		protected Symbol NodeType {
			get { return _nodeType; } 
			set { _nodeType = value; }
		}

		public BaseLexer(ISourceFile source) : base(source) { _source2 = source; }
	
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }
		public IEnumerator<AstNode> GetEnumerator()
		{
			// Start from the beginning
			_inputPosition = 0;
	
			AstNode token;
			while((token = ParseNext()) != null)
				yield return token;
		}

		/// <summary>
		/// This is the most important public function; it determines and returns 
		/// the next token from the input stream.
		/// </summary><returns>Returns the next token, or null if at EOF.</returns>
		public virtual AstNode ParseNext()
		{
			if (_inputPosition >= _source.Count)
				return null;
			
			_nodeType = null;
			_startingPosition = _inputPosition;
			AnyToken();
			SourceRange range = new SourceRange(_source2, _startingPosition, _inputPosition);
			int spaces = 0;
			while (LA(0) == ' ') {
				spaces++;
				_inputPosition++;
			}
			AstNode t = new AstNode(_nodeType, range);
			t.SpacesAfter = spaces;
			
			return t;
		}


		public abstract void AnyToken();

		public ISourceFile SourceFile { get { return _source2; } }
	
		protected override string GetErrorMessage(string expected, int LA)
		{
			return string.Format(
				"Syntax error: in token {0} starting at {1}, got {2} but expected '{3}'", 
				NodeType.Name, _source.IndexToLine(_startingPosition), TokenName(LA), expected);
		}
	}
}
