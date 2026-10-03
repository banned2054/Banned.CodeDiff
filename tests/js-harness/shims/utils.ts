// Re-exports of the real workspace sources that @git-diff-view/utils provides.
// The paths are rewritten by build.mjs (GDV_REPO env var, default the sibling
// clone at C:\Code\JavaScript\git-diff-view) before bundling.
export * from "C:/Code/JavaScript/git-diff-view/packages/utils/src/symbol";
export * from "C:/Code/JavaScript/git-diff-view/packages/utils/src/color";
export * from "C:/Code/JavaScript/git-diff-view/packages/utils/src/highlightAST";
