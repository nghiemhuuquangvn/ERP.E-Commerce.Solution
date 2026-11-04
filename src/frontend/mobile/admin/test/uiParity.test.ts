import fs from 'fs'
import path from 'path'

// The three mobile apps keep one copy each of the component library (no shared package yet). customer/test/parity.test.ts
// guards customer = staff; this test guards admin = staff, so all three stay the same.
const mine = path.resolve(__dirname, '../components/ui')
const theirs = path.resolve(__dirname, '../../staff/components/ui')

const norm = (file: string) => fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n')

describe('component library parity (admin = staff)', () => {
  const files = fs.readdirSync(mine).sort()

  it('has the same files', () => {
    expect(fs.readdirSync(theirs).sort()).toEqual(files)
  })

  it.each(files)('%s is identical', (name) => {
    expect(norm(path.join(theirs, name))).toEqual(norm(path.join(mine, name)))
  })
})
