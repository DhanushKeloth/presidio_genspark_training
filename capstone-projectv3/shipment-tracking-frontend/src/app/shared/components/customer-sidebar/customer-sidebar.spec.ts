import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CustomerSidebar } from './customer-sidebar';

describe('CustomerSidebar', () => {
  let component: CustomerSidebar;
  let fixture: ComponentFixture<CustomerSidebar>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerSidebar],
    }).compileComponents();

    fixture = TestBed.createComponent(CustomerSidebar);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
