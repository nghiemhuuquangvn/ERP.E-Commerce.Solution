import fs from 'fs'
import path from 'path'

// The two mobile apps keep one copy each of the component library (no shared package yet). This test fails when they drift apart.
const mine = path.resolve(__dirname, '../components/ui')
const theirs = path.resolve(__dirname, '../../staff/components/ui')

const norm = (file: string) => fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n')

describe('component library parity (customer = staff)', () => {
  const files = fs.readdirSync(mine).sort()

  it('has the same files', () => {
    expect(fs.readdirSync(theirs).sort()).toEqual(files)
  })

  it.each(files)('%s is identical', (name) => {
    expect(norm(path.join(theirs, name))).toEqual(norm(path.join(mine, name)))
  })
})
