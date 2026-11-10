export const heardQuoteId = 'heard-quote';

// Derived from the quote's own testID, since a sheet can show more than one quote.
export const heardQuoteLabelId = (root: string) => `${root}-label`;
export const heardQuoteMarkId = (root: string) => `${root}-mark`;
export const heardQuoteTextId = (root: string) => `${root}-text`;
export const heardQuoteMissId = (root: string) => `${root}-miss`;
export const heardQuoteFixedFromId = (root: string) => `${root}-fixed-from`;
