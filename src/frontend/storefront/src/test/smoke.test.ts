import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { axe } from './axe'

describe('test setup', () => {
  it('mounts a component with i18n and axe', async () => {
    const wrapper = mount({ template: '<main><h1>{{ $t("nav.brand") }}</h1></main>' }, { attachTo: document.body })
    expect(wrapper.text()).not.toBe('nav.brand')
    expect(await axe(wrapper.element as HTMLElement)).toHaveNoViolations()
    wrapper.unmount()
  })
})
