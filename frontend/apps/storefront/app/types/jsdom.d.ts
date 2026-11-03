// jsdom ships no types, and the one line that uses it needs only this much.
declare module 'jsdom' {
  export class JSDOM {
    constructor(html?: string)
    readonly window: unknown
  }
}
