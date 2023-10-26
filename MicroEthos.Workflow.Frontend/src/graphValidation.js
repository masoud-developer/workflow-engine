export function typesValid(output, input) {
  if (input.type === output.type) {
    return true;
  }
  if (input === 'file') {
    return true;
  }
  return false;
}
