import { z } from 'zod'

/**
 * Everyone gets zod from here, because imports run first: this setting has to be in place before
 * any module creates a schema. Otherwise zod tests whether it may compile validators with
 * `new Function`, which our content security policy forbids on purpose, so that test alone would
 * report a violation on every page with a form.
 */
z.config({ jitless: true })

export { z }
