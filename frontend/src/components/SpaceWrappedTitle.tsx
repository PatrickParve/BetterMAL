import type { ReactNode } from "react";
import "./SpaceWrappedTitle.css";

// A token wider than the line has nowhere to break once it's nowrap, and
// would run outside the page — this never fires for a real anime title, but
// keeps a pathological one on the browser's default breaking instead of
// overflowing (design.md decision 6).
const MAX_NOWRAP_TOKEN_LENGTH = 40;

// No CSS property alone breaks a title at spaces only: word-break: keep-all
// is CJK-only and leaves other scripts at `normal`, and hyphens: none governs
// automatic hyphenation, not the break opportunity a literal "-" creates. So
// each whitespace-delimited token is wrapped in its own nowrap span, joined
// by real spaces — break opportunities then exist only between tokens
// (design.md decision 6).
export function SpaceWrappedTitle({ text }: { text: string }) {
  const words = text.split(/\s+/).filter((word) => word.length > 0);
  const nodes: ReactNode[] = [];
  words.forEach((word, index) => {
    if (index > 0) nodes.push(" ");
    nodes.push(
      word.length > MAX_NOWRAP_TOKEN_LENGTH ? (
        word
      ) : (
        <span key={index} className="space-wrapped-title__word">
          {word}
        </span>
      ),
    );
  });
  return <>{nodes}</>;
}
