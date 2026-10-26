import { expect, test } from 'vitest';
import { imageFiles, insertReferences, pastedNames } from './pastedImages';

const file = (name: string, type = 'image/png') => new File(['x'], name, { type });

test('pastedNames_WhenNoName_ShouldNumberFromOne', () => {
  expect(pastedNames([file(''), file('')], [])).toEqual(['pasted-1.png', 'pasted-2.png']);
});

test('pastedNames_WhenNameTakenOnItem_ShouldSkipToNextFree', () => {
  expect(pastedNames([file(''), file('image.png')], ['pasted-1.png', 'Image.png'])).toEqual([
    'pasted-2.png',
    'pasted-3.png',
  ]);
});

test('pastedNames_WhenNamed_ShouldKeepName', () => {
  expect(pastedNames([file('shot.png')], ['other.png'])).toEqual(['shot.png']);
});

test('pastedNames_WhenJpeg_ShouldUseJpgExtension', () => {
  expect(pastedNames([file('', 'image/jpeg')], [])).toEqual(['pasted-1.jpg']);
});

test('imageFiles_ShouldKeepOnlyImages', () => {
  expect(imageFiles([file('a.png'), file('b.txt', 'text/plain')]).map((f) => f.name)).toEqual([
    'a.png',
  ]);
});

test('insertReferences_ShouldReplaceSelectionAndMoveCursor', () => {
  expect(insertReferences('ab cd', 2, 3, ['x.png'])).toEqual({
    text: 'ab![](x.png)cd',
    cursor: 12,
  });
});

test('insertReferences_WhenTextShrank_ShouldClampToEnd', () => {
  expect(insertReferences('ab', 10, 12, ['x.png']).text).toBe('ab![](x.png)');
});
