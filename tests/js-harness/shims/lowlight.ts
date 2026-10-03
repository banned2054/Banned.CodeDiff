// Minimal stub of @git-diff-view/lowlight: the M1 golden cases never run the
// syntax highlighter (initSyntax / doSyntax are not invoked). Only the module
// shape needs to resolve at bundle time (file.ts imports `highlighter`).
export const highlighter = {
  name: "lowlight-stub",
  type: "class",
  maxLineToIgnoreSyntax: 100000,
  hasRegisteredCurrentLang: () => true,
  getAST: () => null,
  processAST: () => ({ syntaxFileObject: {}, syntaxFileLineNumber: 0 }),
};

export type DiffHighlighter = unknown;

export type DiffHighlighterLang = string;
